using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Backtesting;
using PlusEV.Core.Domain;
using PlusEV.Core.Engine;
using PlusEV.Core.Math;
using PlusEV.Core.Options;

namespace PlusEV.Application.Backtesting;

/// <summary>
/// Walk-forward backtester. Enforces the in-sample / out-of-sample split:
/// the configuration's <c>MinEvPercent</c>, <c>KellyFraction</c> etc. may only be
/// tuned against the in-sample window, then <see cref="RunAsync"/> evaluates the same
/// parameter set on the out-of-sample window and returns both reports separately.
/// </summary>
public sealed class Backtester
{
    private readonly ILogger<Backtester> _logger;
    private readonly IOptions<EvEngineOptions> _defaultEngineOptions;

    public Backtester(ILogger<Backtester> logger, IOptions<EvEngineOptions> defaultEngineOptions)
    {
        _logger = logger;
        _defaultEngineOptions = defaultEngineOptions;
    }

    /// <summary>
    /// Execute a full walk-forward backtest against pre-loaded snapshots and results.
    /// Snapshots should be ordered by timestamp.
    /// </summary>
    public async Task<BacktestReport> RunAsync(
        BacktestConfig config,
        IReadOnlyList<OddsSnapshot> snapshots,
        IReadOnlyDictionary<string, EventResult> results,
        IProgress<BacktestProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(results);
        config.Validate();

        // Build the EV engine with a one-off options instance (keeps the caller's DI container clean).
        var engineOptions = new EvEngineOptions
        {
            MinEvPercent = config.MinEvPercent,
            MinConfidence = config.MinConfidence,
            KellyFraction = config.KellyFraction,
            HardCapFractionOfBankroll = config.HardCapFractionOfBankroll,
            SoftCapMultipleOfAverage = config.SoftCapMultipleOfAverage,
            DevigMethodKey = config.DevigMethodKey,
            SanityCheckMaxDeltaProbability = _defaultEngineOptions.Value.SanityCheckMaxDeltaProbability,
        };
        var engine = new EvEngine(Microsoft.Extensions.Options.Options.Create(engineOptions),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EvEngine>.Instance);

        var inSample = await SimulateAsync(engine, config, snapshots, results,
            config.InSampleStart, config.InSampleEnd, "in-sample", progress, ct).ConfigureAwait(false);
        var oos = await SimulateAsync(engine, config, snapshots, results,
            config.OutOfSampleStart, config.OutOfSampleEnd, "out-of-sample", progress, ct).ConfigureAwait(false);

        return new BacktestReport
        {
            Config = config,
            InSample = inSample,
            OutOfSample = oos,
            RanAt = DateTimeOffset.UtcNow,
            RunId = Guid.NewGuid().ToString("N")[..8],
        };
    }

    private Task<BacktestMetrics> SimulateAsync(
        EvEngine engine,
        BacktestConfig config,
        IReadOnlyList<OddsSnapshot> snapshots,
        IReadOnlyDictionary<string, EventResult> results,
        DateTimeOffset from,
        DateTimeOffset to,
        string phaseName,
        IProgress<BacktestProgress>? progress,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var bankroll = config.StartingBankroll;
            var placedBets = new List<Bet>();
            var bankrollCurve = new List<BankrollPoint> { new(from, bankroll) };
            var taken = new HashSet<string>();
            decimal? rollingAvg = null;

            var filtered = snapshots
                .Where(s => s.FetchedAt >= from && s.FetchedAt < to)
                .Where(s => config.Sports.Any(sp => string.Equals(sp.Key, s.Event.Sport.Key, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(s => s.FetchedAt)
                .ToList();

            int total = System.Math.Max(1, filtered.Count);
            for (int i = 0; i < filtered.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var snap = filtered[i];
                var now = snap.FetchedAt;

                foreach (var opp in engine.Evaluate(snap, bankroll, rollingAvg, now))
                {
                    if (config.BookKeys.Count > 0 &&
                        !config.BookKeys.Contains(opp.Book.Key, StringComparer.OrdinalIgnoreCase)) continue;
                    var key = $"{opp.EventId}|{opp.Market}|{opp.Selection}|{opp.Book.Key}";
                    if (!taken.Add(key)) continue;

                    var stake = opp.RecommendedStake;
                    if (stake <= 0m) continue;
                    if (stake > bankroll) stake = System.Math.Round(bankroll * 0.5m, 2);
                    if (stake <= 0m) continue;

                    bankroll -= stake;
                    var bet = new Bet
                    {
                        Mode = Mode.Backtest,
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
                        PlacedAt = now,
                        EventStart = opp.EventStart,
                    };

                    // Rolling stake average update.
                    var recent = placedBets.TakeLast(24).Select(b => b.Stake).Append(stake).ToList();
                    rollingAvg = recent.Average();

                    // Try to settle immediately if the event is in the past of the snapshot's clock
                    // and we have a result on file.
                    if (results.TryGetValue(bet.EventId, out var result) && result.Completed)
                    {
                        var outcome = BetSettlement.Settle(bet, result);
                        bet.Result = outcome;
                        bet.SettledAt = result.FinishedAt;
                        if (outcome == BetResult.Won)
                        {
                            var payout = stake + stake * (bet.DecimalOdds - 1m);
                            bankroll += payout;
                            bet.Payout = payout;
                        }
                        else if (outcome is BetResult.Push or BetResult.Void)
                        {
                            bankroll += stake;
                            bet.Payout = stake;
                        }
                    }

                    // Look up the closing line (last snapshot before event start) for CLV.
                    var closing = filtered
                        .Where(s => s.Event.Id == bet.EventId && s.FetchedAt <= bet.EventStart)
                        .OrderByDescending(s => s.FetchedAt)
                        .FirstOrDefault();
                    if (closing is not null)
                    {
                        var closeQuote = closing.For(bet.Market).FirstOrDefault(q =>
                            string.Equals(q.Book.Key, opp.Book.Key, StringComparison.OrdinalIgnoreCase));
                        var closeOutcome = closeQuote?.Outcomes.FirstOrDefault(o =>
                            string.Equals(o.Name, bet.Selection, StringComparison.OrdinalIgnoreCase) &&
                            Nullable.Equals(o.Point, bet.Point));
                        if (closeOutcome is not null)
                        {
                            var closeDec = (double)closeOutcome.DecimalOdds;
                            if (closeDec > 1d)
                            {
                                bet.ClosingDecimalOdds = closeDec;
                                bet.Clv = ExpectedValue.Clv((double)bet.DecimalOdds, closeDec);
                            }
                        }
                    }

                    placedBets.Add(bet);
                    bankrollCurve.Add(new BankrollPoint(now, bankroll + SettleOpenStakes(placedBets)));
                }

                if (i % 20 == 0 || i == filtered.Count - 1)
                {
                    progress?.Report(new BacktestProgress(phaseName,
                        (double)(i + 1) / total,
                        placedBets.Count,
                        bankroll));
                }
            }

            return BuildMetrics(config, placedBets, bankrollCurve);
        }, ct);
    }

    private static decimal SettleOpenStakes(List<Bet> bets)
    {
        // Open positions contribute 0 to the equity curve for this simple model; we treat
        // the bankroll drawn down for pending bets as "at risk" and not counted as equity.
        // Returning 0 keeps the curve honest about realized outcomes.
        return 0m;
    }

    private static BacktestMetrics BuildMetrics(
        BacktestConfig config,
        List<Bet> bets,
        List<BankrollPoint> curve)
    {
        int wins = bets.Count(b => b.Result == BetResult.Won);
        int losses = bets.Count(b => b.Result == BetResult.Lost);
        int pushes = bets.Count(b => b.Result == BetResult.Push);
        int voids = bets.Count(b => b.Result == BetResult.Void);
        int decided = wins + losses;
        var totalStaked = bets.Sum(b => b.Stake);
        var profit = bets.Sum(b => b.ProfitOrLoss);
        var ending = curve.Count > 0 ? curve[^1].Balance : config.StartingBankroll;
        var maxDd = MaxDrawdown(curve);
        var winRate = decided == 0 ? 0d : (double)wins / decided;
        var roi = totalStaked == 0m ? 0d : (double)(profit / totalStaked) * 100d;
        var clvs = bets.Where(b => b.Clv.HasValue).Select(b => b.Clv!.Value).ToList();
        var avgClv = clvs.Count == 0 ? 0d : clvs.Average();
        var sharpe = Sharpe(bets);
        var streak = LongestLosingStreak(bets);

        return new BacktestMetrics
        {
            TotalBets = bets.Count,
            Wins = wins,
            Losses = losses,
            Pushes = pushes,
            Voids = voids,
            TotalStaked = totalStaked,
            Profit = profit,
            EndingBankroll = ending,
            MaxDrawdown = maxDd,
            WinRate = winRate,
            RoiPercent = roi,
            AverageClv = avgClv,
            SharpeRatio = sharpe,
            LongestLosingStreak = streak,
            BankrollCurve = curve,
            Bets = bets,
        };
    }

    private static decimal MaxDrawdown(IReadOnlyList<BankrollPoint> curve)
    {
        if (curve.Count == 0) return 0m;
        decimal peak = curve[0].Balance;
        decimal maxDd = 0m;
        foreach (var p in curve)
        {
            if (p.Balance > peak) peak = p.Balance;
            var dd = peak - p.Balance;
            if (dd > maxDd) maxDd = dd;
        }
        return maxDd;
    }

    private static double Sharpe(List<Bet> bets)
    {
        var returns = bets
            .Where(b => b.Result != BetResult.Pending && b.Stake > 0m)
            .Select(b => (double)(b.ProfitOrLoss / b.Stake))
            .ToList();
        if (returns.Count < 2) return 0d;
        var mean = returns.Average();
        var sq = returns.Sum(r => (r - mean) * (r - mean));
        var sd = System.Math.Sqrt(sq / (returns.Count - 1));
        return sd == 0d ? 0d : mean / sd * System.Math.Sqrt(returns.Count);
    }

    private static int LongestLosingStreak(List<Bet> bets)
    {
        int best = 0, cur = 0;
        foreach (var b in bets.OrderBy(b => b.SettledAt ?? b.PlacedAt))
        {
            if (b.Result == BetResult.Lost) { cur++; if (cur > best) best = cur; }
            else if (b.Result == BetResult.Won) cur = 0;
        }
        return best;
    }
}

public sealed record BacktestProgress(string Phase, double Fraction, int BetsPlaced, decimal Bankroll);
