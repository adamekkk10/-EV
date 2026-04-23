namespace PlusEV.Core.Domain;

/// <summary>Result of a bet. <c>Pending</c> is the pre-settlement state.</summary>
public enum BetResult { Pending, Won, Lost, Push, Void }

/// <summary>
/// A placed bet (real or virtual). The <see cref="Mode"/> column makes Live/Demo/Backtest
/// partitions non-mixable even when stored in the same physical table.
/// </summary>
public sealed class Bet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Mode Mode { get; set; }
    public required string EventId { get; set; }
    public required string EventLabel { get; set; }
    public required string Sport { get; set; }
    public required MarketType Market { get; set; }
    public required string Selection { get; set; }
    public double? Point { get; set; }
    public required string BookKey { get; set; }
    public required string BookTitle { get; set; }
    public required decimal DecimalOdds { get; set; }
    public required int AmericanOdds { get; set; }
    public double? TrueProbability { get; set; }
    public double? EvPercentAtEntry { get; set; }
    public double? ConfidenceAtEntry { get; set; }
    public required decimal Stake { get; set; }
    public decimal? Payout { get; set; }
    public double? ClosingDecimalOdds { get; set; }
    public double? Clv { get; set; }
    public BetResult Result { get; set; } = BetResult.Pending;
    public string? Notes { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset EventStart { get; set; }
    public DateTimeOffset? SettledAt { get; set; }

    public decimal ProfitOrLoss => Result switch
    {
        BetResult.Won => Stake * (DecimalOdds - 1m),
        BetResult.Lost => -Stake,
        BetResult.Push or BetResult.Void => 0m,
        _ => 0m,
    };
}
