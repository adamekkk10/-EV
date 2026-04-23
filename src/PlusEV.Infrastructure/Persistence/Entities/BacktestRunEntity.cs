namespace PlusEV.Infrastructure.Persistence.Entities;

/// <summary>A saved backtest result, serialised to JSON for later comparison.</summary>
public sealed class BacktestRunEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public DateTimeOffset RanAt { get; set; }
    public required string ConfigJson { get; set; }
    public required string InSampleMetricsJson { get; set; }
    public required string OutOfSampleMetricsJson { get; set; }
    public double OverfittingGap { get; set; }
    public bool OverfittingWarning { get; set; }
}
