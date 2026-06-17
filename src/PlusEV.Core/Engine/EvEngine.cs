using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Core.Domain;
using PlusEV.Core.Math;

namespace PlusEV.Core.Engine;

/// <summary>
/// The heart of the system. Given an <see cref="OddsSnapshot"/> and a bankroll, produces
/// +EV opportunities using a sharp benchmark (default Pinnacle), devigs it, cross-checks
/// against market consensus, then evaluates every other book's price against the
/// resulting "true" probability.
/// </summary>
public sealed class EvEngine
{
    private readonly EvEngineOptions _options;
    private readonly ILogger<EvEngine> _logger;

    public EvEngine(IOptions<EvEngineOptions> options, ILogger<EvEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate a single snapshot. Returns zero or more opportunities.
    /// </summary>
    /// <param name="snapshot">Odds data for one event.</param>
    /// <param name="bankroll">Current bankroll for stake sizing.</param>
    /// <param name="rollingAverageStake">Rolling average stake for the soft cap.</param>
    /// <param name="now">Injectable clock for deterministic tests.</param>
    public IReadOnlyList<EvOpportunity> Evaluate(
        OddsSnapshot snapshot,
        decimal bankroll,
        decimal? rollingAverageStake,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var devig = DevigMethods.ByKey(_options.DevigMethodKey);

        var results = new List<EvOpportunity>();

        foreach (var market in snapshot.Quotes.Select(q => q.Market).Distinct())
        {
            var quotes = snapshot.For(market).ToList();
            if (quotes.Count == 0) continue;

            var benchmark = SelectBenchmark(quotes);
            if (benchmark is null) continue;

            var benchProbabilities = Devig(benchmark, devig);
            if (benchProbabilities is null) continue;

            // Sanity check against market consensus from the remaining sharp or all books.
            if (!PassesSanityCheck(benchmark, benchProbabilities, quotes, devig))
            {
                _logger.LogDebug("Sanity check failed for {Event} / {Market}; skipping.",
                    snapshot.Event.Matchup, market);
                continue;
            }

            // For each outcome in the benchmark, test every non-benchmark book's price.
            for (int i = 0; i < benchmark.Outcomes.Count; i++)
            {
                var selection = benchmark.Outcomes[i].Name;
                var point = benchmark.Outcomes[i].Point;
                var trueProb = benchProbabilities[i];
                if (trueProb <= 0d || trueProb >= 1d) continue;

                foreach (var quote in quotes)
                {
                    if (ReferenceEquals(quote, benchmark)) continue;
                    var outcome = Match(quote, selection, point);
                    if (outcome is null) continue;

                    var dec = (double)outcome.DecimalOdds;
                    if (dec <= 1d) continue;

                    var ev = ExpectedValue.Compute(trueProb, dec);
                    if (ev < _options.MinEvPercent) continue;

                    var confidence = ConfidenceScore.Compute(new ConfidenceInputs
                    {
                        EvPercent = ev,
                        MarketDepth = quotes.Count,
                        PinnacleOverround = (double)benchmark.Overround,
                        HoursToEvent = (snapshot.Event.Commence - now).TotalHours,
                        LineStabilityStdDev = LineStabilityStdDev(quotes, selection, point, devig, i),
                    });
                    if (confidence < _options.MinConfidence) continue;

                    var stake = Kelly.RecommendedStake(
                        bankroll,
                        trueProb,
                        dec,
                        _options.KellyFraction,
                        _options.HardCapFractionOfBankroll,
                        rollingAverageStake,
                        _options.SoftCapMultipleOfAverage);
                    var kelly = Kelly.Fraction(trueProb, dec);

                    results.Add(new EvOpportunity
                    {
                        EventId = snapshot.Event.Id,
                        EventLabel = snapshot.Event.Matchup,
                        Sport = snapshot.Event.Sport,
                        Market = market,
                        Selection = selection,
                        Point = point,
                        Book = quote.Book,
                        OfferedDecimalOdds = outcome.DecimalOdds,
                        OfferedAmericanOdds = OddsConverter.DecimalToAmerican(outcome.DecimalOdds),
                        TrueProbability = trueProb,
                        EvPercent = ev,
                        Confidence = confidence,
                        RecommendedStake = stake,
                        KellyFraction = kelly,
                        EventStart = snapshot.Event.Commence,
                        IdentifiedAt = now,
                        OddsFetchedAt = quote.FetchedAt == default ? snapshot.FetchedAt : quote.FetchedAt,
                    });
                }
            }
        }

        return results;
    }

    private static MarketQuote? SelectBenchmark(IReadOnlyList<MarketQuote> quotes)
    {
        var pinnacle = quotes.FirstOrDefault(q =>
            string.Equals(q.Book.Key, Bookmaker.Pinnacle.Key, StringComparison.OrdinalIgnoreCase));
        if (pinnacle is not null) return pinnacle;

        // Fallback: average sharp books into a synthetic benchmark.
        var sharps = quotes.Where(q => q.Book.IsSharp).ToList();
        return sharps.Count >= 2 ? BuildSyntheticBenchmark(sharps)
                                 : quotes.OrderBy(q => q.Overround).FirstOrDefault();
    }

    private static MarketQuote BuildSyntheticBenchmark(IReadOnlyList<MarketQuote> sharps)
    {
        // Average the raw implied probabilities per outcome.  Assumes all sharps have the
        // same outcome structure (same sides / point values); skip any that don't match.
        var first = sharps[0];
        var avg = new double[first.Outcomes.Count];
        int count = 0;
        foreach (var q in sharps)
        {
            if (q.Outcomes.Count != first.Outcomes.Count) continue;
            bool aligned = true;
            for (int i = 0; i < q.Outcomes.Count; i++)
            {
                if (!string.Equals(q.Outcomes[i].Name, first.Outcomes[i].Name, StringComparison.OrdinalIgnoreCase) ||
                    q.Outcomes[i].Point != first.Outcomes[i].Point)
                {
                    aligned = false;
                    break;
                }
            }
            if (!aligned) continue;
            for (int i = 0; i < avg.Length; i++) avg[i] += (double)q.Outcomes[i].ImpliedProbability;
            count++;
        }
        if (count == 0) return first;
        for (int i = 0; i < avg.Length; i++) avg[i] /= count;

        var outcomes = new Outcome[first.Outcomes.Count];
        for (int i = 0; i < outcomes.Length; i++)
        {
            outcomes[i] = new Outcome
            {
                Name = first.Outcomes[i].Name,
                DecimalOdds = avg[i] <= 0d ? first.Outcomes[i].DecimalOdds : (decimal)(1d / avg[i]),
                Point = first.Outcomes[i].Point,
            };
        }
        return new MarketQuote
        {
            Book = new Bookmaker("synthetic-sharp-avg", "Sharp consensus", IsSharp: true),
            Market = first.Market,
            Outcomes = outcomes,
            BookUpdatedAt = sharps.Max(s => s.BookUpdatedAt),
            FetchedAt = sharps.Max(s => s.FetchedAt),
        };
    }

    private static double[]? Devig(MarketQuote quote, IDevigMethod method)
    {
        if (quote.Outcomes.Count == 0) return null;
        var raw = new double[quote.Outcomes.Count];
        for (int i = 0; i < raw.Length; i++)
        {
            var dec = (double)quote.Outcomes[i].DecimalOdds;
            if (dec <= 1d) return null;
            raw[i] = 1d / dec;
        }
        try { return method.Devig(raw); }
        catch { return null; }
    }

    private bool PassesSanityCheck(
        MarketQuote benchmark,
        double[] benchProbabilities,
        IReadOnlyList<MarketQuote> quotes,
        IDevigMethod devig)
    {
        // Build a market consensus from every other book that matches the benchmark's
        // outcome structure. If we don't have at least one peer, skip the check.
        var peers = new List<MarketQuote>();
        foreach (var q in quotes)
        {
            if (ReferenceEquals(q, benchmark)) continue;
            if (q.Outcomes.Count != benchmark.Outcomes.Count) continue;
            bool aligned = true;
            for (int i = 0; i < q.Outcomes.Count; i++)
            {
                if (!string.Equals(q.Outcomes[i].Name, benchmark.Outcomes[i].Name, StringComparison.OrdinalIgnoreCase) ||
                    q.Outcomes[i].Point != benchmark.Outcomes[i].Point)
                { aligned = false; break; }
            }
            if (aligned) peers.Add(q);
        }
        if (peers.Count == 0) return true;

        var consensus = new double[benchProbabilities.Length];
        foreach (var q in peers)
        {
            var probs = Devig(q, devig);
            if (probs is null) continue;
            for (int i = 0; i < consensus.Length; i++) consensus[i] += probs[i];
        }
        for (int i = 0; i < consensus.Length; i++) consensus[i] /= peers.Count;

        for (int i = 0; i < consensus.Length; i++)
        {
            if (System.Math.Abs(benchProbabilities[i] - consensus[i]) > _options.SanityCheckMaxDeltaProbability)
                return false;
        }
        return true;
    }

    private static Outcome? Match(MarketQuote quote, string selection, double? point) =>
        quote.Outcomes.FirstOrDefault(o =>
            string.Equals(o.Name, selection, StringComparison.OrdinalIgnoreCase) &&
            Nullable.Equals(o.Point, point));

    private static double LineStabilityStdDev(
        IReadOnlyList<MarketQuote> quotes,
        string selection,
        double? point,
        IDevigMethod devig,
        int outcomeIndex)
    {
        var probs = new List<double>();
        foreach (var q in quotes)
        {
            var dev = Devig(q, devig);
            if (dev is null || dev.Length <= outcomeIndex) continue;
            var match = Match(q, selection, point);
            if (match is null) continue;
            probs.Add(dev[outcomeIndex]);
        }
        if (probs.Count < 2) return 0d;
        var mean = probs.Average();
        var sq = probs.Sum(p => (p - mean) * (p - mean));
        return System.Math.Sqrt(sq / (probs.Count - 1));
    }
}
