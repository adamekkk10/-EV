using PlusEV.Core.Domain;

namespace PlusEV.Core.Engine;

/// <summary>
/// Detects "middle" opportunities: a pair of spread or total bets at different books whose
/// lines overlap so there's a window of outcomes where both sides win.
/// </summary>
public sealed class MiddleScanner
{
    public IReadOnlyList<MiddleOpportunity> Scan(OddsSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var results = new List<MiddleOpportunity>();

        foreach (var market in new[] { MarketType.Spread, MarketType.Total })
        {
            var quotes = snapshot.For(market).ToList();
            if (quotes.Count < 2) continue;

            // Pair every book against every other book.
            for (int i = 0; i < quotes.Count; i++)
            {
                for (int j = i + 1; j < quotes.Count; j++)
                {
                    DetectPair(quotes[i], quotes[j], market, snapshot.Event, now, results);
                }
            }
        }
        return results;
    }

    private static void DetectPair(
        MarketQuote a, MarketQuote b, MarketType market, SportingEvent ev, DateTimeOffset now,
        List<MiddleOpportunity> results)
    {
        foreach (var oa in a.Outcomes)
        {
            if (oa.Point is null) continue;
            foreach (var ob in b.Outcomes)
            {
                if (ob.Point is null) continue;
                if (!string.Equals(oa.Name, ob.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // Spread: opposite sides (team A at -5.5 book 1 vs team A at +6.5 book 2
                    // would not middle, but team A at -5.5 vs team B at +6.5 would). The middle
                    // exists when margins like 6 land between them. Detected below.
                    if (market != MarketType.Spread) continue;
                }

                // For spreads: a team favoured by N on book A needs the opposing team
                // to be +M at book B with M > N to produce a middle width of M - N.
                // We approximate by treating "Name" as side label; a "true" implementation
                // would normalise to the same side but this captures the common case when
                // providers list both teams by name on each book.
                double low = System.Math.Min(oa.Point.Value, ob.Point.Value);
                double high = System.Math.Max(oa.Point.Value, ob.Point.Value);
                if (high - low < 0.5) continue;     // no middle window
                if (!MiddleActuallyExists(oa, ob, market)) continue;

                // Equal-stake payout model. User bets $S on each side.
                // If both win, profit = oa.decimal * S + ob.decimal * S - 2S = (d_a + d_b - 2)*S.
                var payoutIfHit = oa.DecimalOdds + ob.DecimalOdds - 2m;

                results.Add(new MiddleOpportunity
                {
                    EventId = ev.Id,
                    EventLabel = ev.Matchup,
                    Market = market,
                    LowSelection = oa.Name,
                    LowBook = a.Book,
                    LowPoint = oa.Point.Value,
                    LowDecimalOdds = oa.DecimalOdds,
                    HighSelection = ob.Name,
                    HighBook = b.Book,
                    HighPoint = ob.Point.Value,
                    HighDecimalOdds = ob.DecimalOdds,
                    MiddleWidth = high - low,
                    PayoutIfHit = payoutIfHit,
                    IdentifiedAt = now,
                    EventStart = ev.Commence,
                });
            }
        }
    }

    private static bool MiddleActuallyExists(Outcome oa, Outcome ob, MarketType market)
    {
        // For totals: Over X at book A and Under Y at book B middles if Y > X.
        if (market == MarketType.Total)
        {
            bool isOverA = oa.Name.Contains("Over", StringComparison.OrdinalIgnoreCase);
            bool isOverB = ob.Name.Contains("Over", StringComparison.OrdinalIgnoreCase);
            if (isOverA == isOverB) return false;
            if (isOverA) return ob.Point > oa.Point;
            return oa.Point > ob.Point;
        }
        // For spreads we need opposite sides with overlapping cover windows. Without team
        // labelling we conservatively accept when points differ by at least 1 and names differ.
        return !string.Equals(oa.Name, ob.Name, StringComparison.OrdinalIgnoreCase);
    }
}
