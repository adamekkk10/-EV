using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Domain;
using PlusEV.Core.Math;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.Infrastructure.Alerts;

/// <summary>
/// Background-queue implementation of <see cref="IAlertDispatcher"/>. Enqueue is
/// non-blocking; a single background worker drains the channel and fans out to
/// every <see cref="IAlertChannel"/>. Failures in one channel don't affect others.
/// </summary>
public sealed class AlertDispatcher : IAlertDispatcher, IHostedService, IAsyncDisposable
{
    private readonly Channel<AlertMessage> _queue = Channel.CreateUnbounded<AlertMessage>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly IEnumerable<IAlertChannel> _channels;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertDispatcher> _logger;
    private CancellationTokenSource? _cts;
    private Task? _worker;

    public AlertDispatcher(
        IEnumerable<IAlertChannel> channels,
        IServiceScopeFactory scopeFactory,
        ILogger<AlertDispatcher> logger)
    {
        _channels = channels;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task EnqueueAsync(AlertMessage message, CancellationToken ct = default)
    {
        if (!_queue.Writer.TryWrite(message))
        {
            _logger.LogWarning("Alert queue is full; dropping {Title}", message.Title);
        }
        return Task.CompletedTask;
    }

    public AlertMessage FromOpportunity(EvOpportunity opp)
    {
        var evPct = (opp.EvPercent * 100d).ToString("F2");
        var body = $"{opp.Sport.Title} — {opp.EventLabel}\n" +
                   $"{opp.Market} · {opp.Selection} @ {OddsConverter.FormatAmerican(opp.OfferedAmericanOdds)} ({opp.Book.Title})\n" +
                   $"EV +{evPct}% · confidence {opp.Confidence:F0} · stake ${opp.RecommendedStake:F2}\n" +
                   $"Kick: {opp.EventStart.LocalDateTime:g}";
        return new AlertMessage(
            $"+EV opportunity ({evPct}%)",
            body,
            opp.EvPercent >= 0.05d ? AlertSeverity.Success : AlertSeverity.Info,
            AlertCategory.Opportunity,
            DateTimeOffset.UtcNow);
    }

    public AlertMessage FromArbitrage(ArbitrageOpportunity arb)
    {
        var pct = (arb.GuaranteedReturnPercent * 100m).ToString("F2");
        var legs = string.Join(" · ", arb.Legs.Select(l =>
            $"{l.Selection} @ {l.DecimalOdds:F2} ({l.Book.Title})"));
        return new AlertMessage(
            $"Arbitrage {pct}%",
            $"{arb.EventLabel} — {arb.Market}\n{legs}",
            AlertSeverity.Success,
            AlertCategory.Arbitrage,
            DateTimeOffset.UtcNow);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _worker = Task.Run(() => WorkerLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        _cts?.Cancel();
        if (_worker is not null)
        {
            try { await _worker.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(ct))
        {
            foreach (var channel in _channels)
            {
                if (!channel.ShouldSend(message)) continue;
                var delivered = false;
                string? error = null;
                try
                {
                    await channel.SendAsync(message, ct).ConfigureAwait(false);
                    delivered = true;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    error = ex.Message;
                    _logger.LogWarning(ex, "Channel {Channel} failed to deliver", channel.Key);
                }
                await LogAsync(channel, message, delivered, error).ConfigureAwait(false);
            }
        }
    }

    private async Task LogAsync(IAlertChannel channel, AlertMessage message, bool delivered, string? error)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PlusEvDbContext>();
            db.AlertLog.Add(new AlertLogEntry
            {
                Channel = channel.Key,
                Title = message.Title,
                Body = message.Body,
                Severity = message.Severity.ToString(),
                Category = message.Category.ToString(),
                Delivered = delivered,
                Error = error,
                SentAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to persist alert log (non-fatal)");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _cts?.Dispose();
    }
}
