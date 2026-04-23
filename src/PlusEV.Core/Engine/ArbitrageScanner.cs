using PlusEV.Core.Domain;

namespace PlusEV.Core.Engine;

/// <summary>
/// Detects arbitrage on a single snapshot: for each market, find the best price per outcome
/// across all books and check whether the sum of implied probabilities is less than 1.
/// </summary>
public sealed class ArbitrageScanner
{
    public IReadOnlyList<ArbitrageOpportunity> Scan(OddsSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var results = new List<ArbitrageOpportunity>();

        foreach (var market in snapshot.Quotes.Select(q => q.Market).Distinct())
        {
            var quotes = snapshot.For(market).ToList();
            if (quotes.Count == 0) continue;

            // Arb requires that every outcome be available somewhere. Use the first quote
            // as the outcome template.
            var template = quotes[0];

            // Group by (name, point) so spreads/totals with differing lines don't cross-talk.
            var groups = new Dictionary<(string Name, double? Point), (decimal BestDec, Bookmaker Book)>(
                new OutcomeKeyComparer());

            foreach (var q in quotes)
            {
                foreach (var o in q.Outcomes)
                {
                    var key = (o.Name, o.Point);
                    if (!groups.TryGetValue(key, out var current) || o.DecimalOdds > current.BestDec)
                        groups[key] = (o.DecimalOdds, q.Book);
                }
            }

            // Only consider outcome-complete groups that match the template structure.
            if (template.Outcomes.Count < 2) continue;
            var legs = new List<ArbitrageLeg>();
            double impliedSum = 0d;
            foreach (var o in template.Outcomes)
            {
                if (!groups.TryGetValue((o.Name, o.Point), out var best)) { legs = null!; break; }
                impliedSum += 1d / (double)best.BestDec;
                legs.Add(new ArbitrageLeg
                {
                    Selection = o.Name,
                    Book = best.Book,
                    DecimalOdds = best.BestDec,
                    StakeFraction = 0m, // filled below
                });
            }
            if (legs is null || impliedSum >= 1d) continue;

            // Stake fractions so profit is equal across outcomes. Stake_i = (1/dec_i) / Σ(1/dec).
            var fractions = new List<ArbitrageLeg>(legs.Count);
            foreach (var leg in legs)
            {
                fractions.Add(new ArbitrageLeg
                {
                    Selection = leg.Selection,
                    Book = leg.Book,
                    DecimalOdds = leg.DecimalOdds,
                    StakeFraction = (decimal)((1d / (double)leg.DecimalOdds) / impliedSum),
                });
            }

            results.Add(new ArbitrageOpportunity
            {
                EventId = snapshot.Event.Id,
                EventLabel = snapshot.Event.Matchup,
                Market = market,
                Legs = fractions,
                GuaranteedReturnPercent = (decimal)(1d / impliedSum - 1d),
                IdentifiedAt = now,
                EventStart = snapshot.Event.Commence,
            });
        }

        return results;
    }

    private sealed class OutcomeKeyComparer : IEqualityComparer<(string Name, double? Point)>
    {
        public bool Equals((string Name, double? Point) x, (string Name, double? Point) y) =>
            string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
            Nullable.Equals(x.Point, y.Point);

        public int GetHashCode((string Name, double? Point) obj) =>
            HashCode.Combine(obj.Name.ToLowerInvariant(), obj.Point);
    }
}
