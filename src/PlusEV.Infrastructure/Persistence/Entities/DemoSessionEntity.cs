namespace PlusEV.Infrastructure.Persistence.Entities;

/// <summary>A demo bot session. Every bet placed during the session references its id for replay.</summary>
public sealed class DemoSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public decimal StartingBankroll { get; set; }
    public decimal? EndingBankroll { get; set; }
    public string Aggressiveness { get; set; } = "Balanced";
    public int BetsPlaced { get; set; }
    public int BetsWon { get; set; }
    public int BetsLost { get; set; }
    public string? Notes { get; set; }
}
