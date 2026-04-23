namespace PlusEV.Core.Domain;

/// <summary>
/// A +EV betting opportunity identified by the engine. Immutable.
/// </summary>
public sealed class EvOpportunity
{
    public required string EventId { get; init; }
    public required string EventLabel { get; init; }
    public required Sport Sport { get; init; }
    public required MarketType Market { get; init; }
    public required string Selection { get; init; }
    public double? Point { get; init; }
    public required Bookmaker Book { get; init; }
    public required decimal OfferedDecimalOdds { get; init; }
    public required int OfferedAmericanOdds { get; init; }
    public required double TrueProbability { get; init; }
    public required double EvPercent { get; init; }
    public required double Confidence { get; init; }
    public required decimal RecommendedStake { get; init; }
    public required double KellyFraction { get; init; }
    public required DateTimeOffset EventStart { get; init; }
    public required DateTimeOffset IdentifiedAt { get; init; }
    public required DateTimeOffset OddsFetchedAt { get; init; }

    public TimeSpan DetectionLatency => IdentifiedAt - OddsFetchedAt;
    public TimeSpan TimeToEvent => EventStart - IdentifiedAt;
}
