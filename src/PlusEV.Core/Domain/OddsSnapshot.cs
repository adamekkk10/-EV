namespace PlusEV.Core.Domain;

/// <summary>
/// The full set of quotes across all books for a single event at a single fetch.
/// This is the unit produced by <c>IOddsProvider</c> and stored for line-movement tracking.
/// </summary>
public sealed class OddsSnapshot
{
    public required SportingEvent Event { get; init; }
    public required IReadOnlyList<MarketQuote> Quotes { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }

    public MarketQuote? Pinnacle(MarketType market) =>
        Quotes.FirstOrDefault(q => q.Market == market &&
                                   string.Equals(q.Book.Key, Bookmaker.Pinnacle.Key, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<MarketQuote> For(MarketType market) => Quotes.Where(q => q.Market == market);
}
