namespace PlusEV.Core.Domain;

/// <summary>
/// A single selection with its offered price. Prices are stored in decimal form
/// internally; convert at the UI boundary via <see cref="Math.OddsConverter"/>.
/// </summary>
public sealed class Outcome
{
    public required string Name { get; init; }

    /// <summary>Decimal price e.g. 1.91 for -110.</summary>
    public required decimal DecimalOdds { get; init; }

    /// <summary>For spreads/totals, the point value; null otherwise.</summary>
    public double? Point { get; init; }

    public decimal ImpliedProbability => DecimalOdds == 0 ? 0 : 1m / DecimalOdds;
}
