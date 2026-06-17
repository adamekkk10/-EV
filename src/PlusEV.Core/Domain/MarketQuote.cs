namespace PlusEV.Core.Domain;

/// <summary>
/// A bookmaker's full two- or three-way line on a single market at a moment in time.
/// </summary>
public sealed class MarketQuote
{
    public required Bookmaker Book { get; init; }
    public required MarketType Market { get; init; }
    public required IReadOnlyList<Outcome> Outcomes { get; init; }

    /// <summary>When the book last updated this line (provider timestamp).</summary>
    public DateTimeOffset BookUpdatedAt { get; init; }

    /// <summary>When we fetched it. Used with <see cref="BookUpdatedAt"/> for latency tracking.</summary>
    public DateTimeOffset FetchedAt { get; init; }

    /// <summary>
    /// Sum of naive implied probabilities. A two-way market with overround 1.05 has ~5% vig.
    /// </summary>
    public decimal Overround => Outcomes.Sum(o => o.ImpliedProbability);

    /// <summary>Vig expressed as a fraction (0.045 = 4.5%).</summary>
    public decimal Vig => Overround - 1m;
}
