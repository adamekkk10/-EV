using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Domain;
using PlusEV.Core.Engine;
using PlusEV.Core.Options;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.Application.Services;

/// <summary>
/// Background service that polls the odds provider, runs the EV engine + arb/middle
/// scanners, writes line-history snapshots, updates <see cref="OpportunityFeed"/>, and
/// enqueues alerts.  Everything that needs a scoped EF Core context creates its own scope.
/// </summary>
public sealed class OddsIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOddsProvider _provider;
    private readonly EvEngine _evEngine;
    private readonly ArbitrageScanner _arbScanner;
    private readonly MiddleScanner _middleScanner;
    private readonly OpportunityFeed _feed;
    private readonly LatencyTracker _latency;
    private readonly IAlertDispatcher _alerts;
    private readonly IOptionsMonitor<OddsApiOptions> _apiOptions;
    private readonly IOptionsMonitor<LineHistoryOptions> _historyOptions;
    private readonly ILogger<OddsIngestionService> _logger;

    public OddsIngestionService(
        IServiceScopeFactory scopeFactory,
        IOddsProvider provider,
        EvEngine evEngine,
        ArbitrageScanner arbScanner,
        MiddleScanner middleScanner,
        OpportunityFeed feed,
        LatencyTracker latency,
        IAlertDispatcher alerts,
        IOptionsMonitor<OddsApiOptions> apiOptions,
        IOptionsMonitor<LineHistoryOptions> historyOptions,
        ILogger<OddsIngestionService> logger)
    {
        _scopeFactory = scopeFactory;
        _provider = provider;
        _evEngine = evEngine;
        _arbScanner = arbScanner;
        _middleScanner = middleScanner;
        _feed = feed;
        _latency = latency;
        _alerts = alerts;
        _apiOptions = apiOptions;
        _historyOptions = historyOptions;
        _logger = logger;
    }

    /// <summary>Sports we currently poll. The UI updates this list at runtime.</summary>
    public HashSet<string> ActiveSportKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Books we want odds from. Empty = provider default.</summary>
    public HashSet<string> ActiveBookKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    public MarketType[] ActiveMarkets { get; set; } = { MarketType.H2H, MarketType.Spread, MarketType.Total };

    public DateTimeOffset? LastSuccessfulFetch { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first snapshot-history flush timer; we don't write raw history every poll.
        var nextHistoryWrite = DateTimeOffset.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(nextHistoryWrite <= DateTimeOffset.UtcNow, stoppingToken).ConfigureAwait(false);
                if (nextHistoryWrite <= DateTimeOffset.UtcNow)
                    nextHistoryWrite = DateTimeOffset.UtcNow + _historyOptions.CurrentValue.SnapshotInterval;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Odds ingestion iteration failed");
            }
            await Task.Delay(_apiOptions.CurrentValue.PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PollOnceAsync(bool persistHistory, CancellationToken ct)
    {
        if (ActiveSportKeys.Count == 0)
        {
            _logger.LogDebug("No active sports configured; sleeping.");
            return;
        }

        var allEvs = new List<EvOpportunity>();
        var allArbs = new List<ArbitrageOpportunity>();
        var allMiddles = new List<MiddleOpportunity>();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlusEvDbContext>();
        var bankroll = scope.ServiceProvider.GetRequiredService<BankrollService>();

        var liveBankroll = await bankroll.GetCurrentBalanceAsync(Mode.Live, ct).ConfigureAwait(false);
        if (liveBankroll <= 0m) liveBankroll = 1000m; // sensible default for display / sizing math.
        var rollingAvg = await bankroll.RollingAverageStakeAsync(Mode.Live, ct: ct).ConfigureAwait(false);

        foreach (var sportKey in ActiveSportKeys.ToList())
        {
            var sport = new Sport(sportKey, sportKey);
            IReadOnlyList<OddsSnapshot> snapshots;
            try
            {
                snapshots = await _provider.GetOddsAsync(
                    sport, ActiveMarkets, ActiveBookKeys, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetOddsAsync failed for sport {Sport}", sportKey);
                continue;
            }

            foreach (var snap in snapshots)
            {
                _latency.RecordFetch(snap.FetchedAt - (snap.Quotes.FirstOrDefault()?.BookUpdatedAt ?? snap.FetchedAt));

                var now = DateTimeOffset.UtcNow;
                var evs = _evEngine.Evaluate(snap, liveBankroll, rollingAvg, now);
                foreach (var e in evs) _latency.RecordIdentify(e.DetectionLatency);
                allEvs.AddRange(evs);

                var arbs = _arbScanner.Scan(snap, now);
                allArbs.AddRange(arbs);

                var middles = _middleScanner.Scan(snap, now);
                allMiddles.AddRange(middles);

                if (persistHistory)
                {
                    foreach (var q in snap.Quotes)
                    {
                        foreach (var o in q.Outcomes)
                        {
                            db.OddsHistory.Add(new OddsHistoryPoint
                            {
                                Mode = Mode.Live,
                                EventId = snap.Event.Id,
                                Sport = snap.Event.Sport.Key,
                                Market = q.Market,
                                BookKey = q.Book.Key,
                                Selection = o.Name,
                                Point = o.Point,
                                DecimalOdds = o.DecimalOdds,
                                BookUpdatedAt = q.BookUpdatedAt,
                                FetchedAt = q.FetchedAt,
                                FetchLatencyMs = (long)(q.FetchedAt - q.BookUpdatedAt).TotalMilliseconds,
                            });
                        }
                    }
                }
            }
        }

        if (persistHistory)
        {
            try { await db.SaveChangesAsync(ct).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to persist odds history"); }
        }

        _feed.Replace(allEvs, allArbs, allMiddles);
        LastSuccessfulFetch = DateTimeOffset.UtcNow;

        // Fan out to alerts. Channel-level filters decide which actually fire.
        foreach (var e in allEvs) await _alerts.EnqueueAsync(_alerts.FromOpportunity(e), ct).ConfigureAwait(false);
        foreach (var a in allArbs) await _alerts.EnqueueAsync(_alerts.FromArbitrage(a), ct).ConfigureAwait(false);
    }
}
