namespace PlusEV.Core.Domain;

/// <summary>
/// A pair of outcomes at different books whose combined implied probability is less
/// than 1 — i.e. a guaranteed profit ("arb") if both sides can be taken in time.
/// </summary>
public sealed class ArbitrageOpportunity
{
    public required string EventId { get; init; }
    public required string EventLabel { get; init; }
    public required MarketType Market { get; init; }
    public required IReadOnlyList<ArbitrageLeg> Legs { get; init; }
    public required decimal GuaranteedReturnPercent { get; init; }
    public required DateTimeOffset IdentifiedAt { get; init; }
    public required DateTimeOffset EventStart { get; init; }
}

public sealed class ArbitrageLeg
{
    public required string Selection { get; init; }
    public required Bookmaker Book { get; init; }
    public required decimal DecimalOdds { get; init; }

    /// <summary>Stake normalised so total stake across legs equals 1.</summary>
    public required decimal StakeFraction { get; init; }
}
