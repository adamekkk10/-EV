namespace PlusEV.Core.Domain;

/// <summary>
/// A ledger entry on the bankroll. Computed P/L from settled bets is written in as
/// <c>BetSettlement</c>; manual adjustments (deposits/withdrawals) use <c>Adjustment</c>.
/// </summary>
public sealed class BankrollEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Mode Mode { get; set; }
    public required BankrollEntryKind Kind { get; set; }
    public required decimal Amount { get; set; }
    public required decimal BalanceAfter { get; set; }
    public Guid? RelatedBetId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public enum BankrollEntryKind { Initial, Deposit, Withdrawal, Adjustment, BetStaked, BetSettlement }
