using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Application.Services;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Backtesting;
using PlusEV.Core.Domain;
using PlusEV.Core.Math;
using PlusEV.Core.Options;
using PlusEV.Infrastructure.Persistence;
using PlusEV.Infrastructure.Persistence.Entities;

namespace PlusEV.Application.Demo;

/// <summary>
/// Demo / paper-trading "bot". Subscribes to <see cref="OpportunityFeed"/>, auto-places
/// virtual bets that pass its filters, and settles them when real results arrive.
/// All state is partitioned under <see cref="Mode.Demo"/>.
/// </summary>
public sealed class DemoBot
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OpportunityFeed _feed;
    private readonly IEventResultProvider _results;
    private readonly IAlertDispatcher _alerts;
    private readonly IOptionsMonitor<DemoBotOptions> _options;
    private readonly ILogger<DemoBot> _logger;

    private readonly object _lock = new();
    private bool _running;
    private CancellationTokenSource? _cts;
    private Guid? _sessionId;
    private readonly HashSet<string> _takenOpportunityKeys = new(); // dedupe per session

    /// <summary>Observable activity feed for the Demo Bot view.</summary>
    public ObservableCollection<DemoActivityEntry> Activity { get; } = new();

    /// <summary>Fires whenever the demo bankroll changes so the UI can animate.</summary>
    public event EventHandler<decimal>? BankrollChanged;

    public bool IsRunning { get { lock (_lock) return _running; } }

    public DemoBot(
        IServiceScopeFactory scopeFactory,
        OpportunityFeed feed,
        IEventResultProvider results,
        IAlertDispatcher alerts,
        IOptionsMonitor<DemoBotOptions> options,
        ILogger<DemoBot> logger)
    {
        _scopeFactory = scopeFactory;
        _feed = feed;
        _results = results;
        _alerts = alerts;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(decimal? startingBankroll = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_running) return;
            _running = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlusEvDbContext>();
        var bankroll = scope.ServiceProvider.GetRequiredService<BankrollService>();

        var start = startingBankroll ?? 1000m;
        await bankroll.EnsureInitialAsync(Mode.Demo, start, ct).ConfigureAwait(false);

        var session = new DemoSessionEntity
        {
            StartedAt = DateTimeOffset.UtcNow,
            StartingBankroll = await bankroll.GetCurrentBalanceAsync(Mode.Demo, ct).ConfigureAwait(false),
            Aggressiveness = _options.CurrentValue.Aggressiveness.ToString(),
        };
        db.DemoSessions.Add(session);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        _sessionId = session.Id;
        _takenOpportunityKeys.Clear();

        Log(new DemoActivityEntry(DateTimeOffset.UtcNow, "▶️ Demo bot started",
            DemoActivityKind.SessionStart, null));

        _feed.Updated += OnFeedUpdated;
        _ = Task.Run(() => SettlementLoopAsync(_cts!.Token), _cts!.Token);

        _logger.LogInformation("Demo bot session {Id} started", session.Id);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        CancellationTokenSource? cts;
        Guid? sessionId;
        lock (_lock)
        {
            if (!_running) return;
            _running = false;
            cts = _cts;
            sessionId = _sessionId;
            _sessionId = null;
        }

        _feed.Updated -= OnFeedUpdated;
        cts?.Cancel();
        Log(new DemoActivityEntry(DateTimeOffset.UtcNow, "⏹️ Demo bot stopped",
            DemoActivityKind.SessionStop, null));

        if (sessionId is { } id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PlusEvDbContext>();
            var bankroll = scope.ServiceProvider.GetRequiredService<BankrollService>();
            var session = await db.DemoSessions.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
            if (session is not null)
            {
                session.EndedAt = DateTimeOffset.UtcNow;
                session.EndingBankroll = await bankroll.GetCurrentBalanceAsync(Mode.Demo, ct).ConfigureAwait(false);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }
    }

    public async Task ResetAsync(decimal newStarting, CancellationToken ct = default)
    {
        await StopAsync(ct).ConfigureAwait(false);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlusEvDbContext>();
        var demoBets = db.Bets.Where(b => b.Mode == Mode.Demo);
        db.Bets.RemoveRange(demoBets);
        var demoLedger = db.BankrollEntries.Where(e => e.Mode == Mode.Demo);
        db.BankrollEntries.RemoveRange(demoLedger);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        Activity.Clear();
        await StartAsync(newStarting, ct).ConfigureAwait(false);
    }

    private void OnFeedUpdated(object? sender, EventArgs e)
    {
        if (!IsRunning) return;
        _ = Task.Run(() => ConsiderOpportunitiesAsync(_cts!.Token));
    }

    private async Task ConsiderOpportunitiesAsync(CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        var threshold = opts.MinEvPercent;
        if (opts.Aggressiveness != DemoBotAggressiveness.Balanced)
        {
            var preset = DemoBotOptions.PresetFor(opts.Aggressiveness);
            threshold = preset.MinEv;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlusEvDbContext>();
        var tracker = scope.ServiceProvider.GetRequiredService<BetTracker>();
        var bankroll = scope.ServiceProvider.GetRequiredService<BankrollService>();

        var openCount = await db.Bets
            .CountAsync(b => b.Mode == Mode.Demo && b.Result == BetResult.Pending, ct).ConfigureAwait(false);
        if (openCount >= opts.MaxOpenPositions) return;

        foreach (var opp in _feed.Items.ToList())
        {
            if (ct.IsCancellationRequested) break;
            if (opp.EvPercent < threshold) continue;
            if (opts.SportKeys.Count > 0 &&
                !opts.SportKeys.Contains(opp.Sport.Key, StringComparer.OrdinalIgnoreCase)) continue;
            if (opts.BookKeys.Count > 0 &&
                !opts.BookKeys.Contains(opp.Book.Key, StringComparer.OrdinalIgnoreCase)) continue;

            var key = $"{opp.EventId}|{opp.Market}|{opp.Selection}|{opp.Book.Key}";
            if (!_takenOpportunityKeys.Add(key)) continue;

            var balance = await bankroll.GetCurrentBalanceAsync(Mode.Demo, ct).ConfigureAwait(false);
            var rolling = await bankroll.RollingAverageStakeAsync(Mode.Demo, ct: ct).ConfigureAwait(false);
            var stake = Kelly.RecommendedStake(
                balance,
                opp.TrueProbability,
                (double)opp.OfferedDecimalOdds,
                opts.KellyFraction,
                opts.HardCapFractionOfBankroll,
                rolling);
            if (stake <= 0m) continue;

            var bet = new Bet
            {
                Mode = Mode.Demo,
                EventId = opp.EventId,
                EventLabel = opp.EventLabel,
                Sport = opp.Sport.Key,
                Market = opp.Market,
                Selection = opp.Selection,
                Point = opp.Point,
                BookKey = opp.Book.Key,
                BookTitle = opp.Book.Title,
                DecimalOdds = opp.OfferedDecimalOdds,
                AmericanOdds = opp.OfferedAmericanOdds,
                TrueProbability = opp.TrueProbability,
                EvPercentAtEntry = opp.EvPercent,
                ConfidenceAtEntry = opp.Confidence,
                Stake = stake,
                PlacedAt = DateTimeOffset.UtcNow,
                EventStart = opp.EventStart,
                Notes = $"Demo session {_sessionId}",
            };
            await tracker.RecordAsync(bet, ct).ConfigureAwait(false);

            Log(new DemoActivityEntry(
                DateTimeOffset.UtcNow,
                $"✅ Bet placed: {opp.Selection} @ {opp.OfferedDecimalOdds:F2} on {opp.Book.Title}, " +
                $"stake ${stake:F2}, EV +{opp.EvPercent * 100:F2}%, confidence {opp.Confidence:F0}",
                DemoActivityKind.BetPlaced,
                bet.Id));
            BankrollChanged?.Invoke(this, balance - stake);
            openCount++;
            if (openCount >= opts.MaxOpenPositions) break;
        }
    }

    private async Task SettlementLoopAsync(CancellationToken ct)
    {
        var pollInterval = TimeSpan.FromMinutes(3);
        while (!ct.IsCancellationRequested)
        {
            try { await SettleOnceAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Demo settlement iteration failed"); }
            try { await Task.Delay(pollInterval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SettleOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlusEvDbContext>();
        var tracker = scope.ServiceProvider.GetRequiredService<BetTracker>();
        var bankroll = scope.ServiceProvider.GetRequiredService<BankrollService>();

        var pending = await db.Bets
            .Where(b => b.Mode == Mode.Demo && b.Result == BetResult.Pending && b.EventStart < DateTimeOffset.UtcNow)
            .ToListAsync(ct).ConfigureAwait(false);
        if (pending.Count == 0) return;

        foreach (var g in pending.GroupBy(p => p.Sport))
        {
            var sport = new Sport(g.Key, g.Key);
            IReadOnlyList<EventResult> results;
            try { results = await _results.GetResultsAsync(sport, DateTimeOffset.UtcNow.AddDays(-1), ct).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogWarning(ex, "Result fetch failed for {Sport}", g.Key); continue; }

            foreach (var bet in g)
            {
                var match = results.FirstOrDefault(r => r.EventId == bet.EventId && r.Completed);
                if (match is null) continue;
                var outcome = BetSettlement.Settle(bet, match);
                if (outcome == BetResult.Pending) continue;

                await tracker.SettleAsync(bet.Id, outcome, null, ct).ConfigureAwait(false);
                var msg = outcome == BetResult.Won
                    ? $"🏁 Bet settled: WIN, +${bet.Stake * (bet.DecimalOdds - 1m):F2}"
                    : outcome == BetResult.Lost
                        ? $"🏁 Bet settled: LOSS, -${bet.Stake:F2}"
                        : $"🏁 Bet settled: {outcome}";
                Log(new DemoActivityEntry(DateTimeOffset.UtcNow, msg, DemoActivityKind.BetSettled, bet.Id));
                BankrollChanged?.Invoke(this, await bankroll.GetCurrentBalanceAsync(Mode.Demo, ct).ConfigureAwait(false));
            }
        }
    }

    private void Log(DemoActivityEntry entry)
    {
        Activity.Add(entry);
        while (Activity.Count > 500) Activity.RemoveAt(0);
    }
}

public enum DemoActivityKind { SessionStart, SessionStop, Scan, BetPlaced, BetSettled, Error }

public sealed record DemoActivityEntry(DateTimeOffset At, string Text, DemoActivityKind Kind, Guid? BetId);
