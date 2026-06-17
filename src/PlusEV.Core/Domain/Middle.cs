namespace PlusEV.Core.Domain;

/// <summary>
/// A "middle" setup: two spreads or totals at different books that leave a window where
/// BOTH sides win. e.g. -5.5 at one book and +6.5 at another, middle window = a 6-point margin.
/// </summary>
public sealed class MiddleOpportunity
{
    public required string EventId { get; init; }
    public required string EventLabel { get; init; }
    public required MarketType Market { get; init; }
    public required string LowSelection { get; init; }
    public required Bookmaker LowBook { get; init; }
    public required double LowPoint { get; init; }
    public required decimal LowDecimalOdds { get; init; }
    public required string HighSelection { get; init; }
    public required Bookmaker HighBook { get; init; }
    public required double HighPoint { get; init; }
    public required decimal HighDecimalOdds { get; init; }

    /// <summary>Width of the middle window in points (e.g. 1.0 for -5.5 / +6.5).</summary>
    public required double MiddleWidth { get; init; }

    /// <summary>Estimated probability of landing in the middle (if known).</summary>
    public double? ImpliedMiddleProbability { get; init; }

    /// <summary>Payout if middle hits (profit as a multiple of total stake).</summary>
    public required decimal PayoutIfHit { get; init; }
    public required DateTimeOffset IdentifiedAt { get; init; }
    public required DateTimeOffset EventStart { get; init; }
}
