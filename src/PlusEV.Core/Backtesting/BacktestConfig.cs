using PlusEV.Core.Domain;

namespace PlusEV.Core.Backtesting;

/// <summary>
/// Full parameter set for a backtest run. Walk-forward is enforced: both
/// <see cref="InSampleStart"/>–<see cref="InSampleEnd"/> and
/// <see cref="OutOfSampleStart"/>–<see cref="OutOfSampleEnd"/> must be supplied before
/// the engine will run, and parameter tuning is only allowed on in-sample data.
/// </summary>
public sealed class BacktestConfig
{
    public required DateTimeOffset InSampleStart { get; init; }
    public required DateTimeOffset InSampleEnd { get; init; }
    public required DateTimeOffset OutOfSampleStart { get; init; }
    public required DateTimeOffset OutOfSampleEnd { get; init; }

    public required IReadOnlyList<Sport> Sports { get; init; }
    public required IReadOnlyList<string> BookKeys { get; init; }
    public required IReadOnlyList<MarketType> Markets { get; init; }

    public double MinEvPercent { get; init; } = 0.02d;
    public double MinConfidence { get; init; } = 40d;
    public double KellyFraction { get; init; } = 0.25d;
    public double HardCapFractionOfBankroll { get; init; } = 0.02d;
    public double SoftCapMultipleOfAverage { get; init; } = 5d;
    public string DevigMethodKey { get; init; } = "power";
    public decimal StartingBankroll { get; init; } = 1000m;

    public string? RunName { get; init; }

    public void Validate()
    {
        if (InSampleStart >= InSampleEnd)
            throw new InvalidOperationException("In-sample range invalid.");
        if (OutOfSampleStart >= OutOfSampleEnd)
            throw new InvalidOperationException("Out-of-sample range invalid.");
        if (OutOfSampleStart < InSampleEnd)
            throw new InvalidOperationException(
                "Walk-forward violated: out-of-sample range must start at or after in-sample end.");
        if (StartingBankroll <= 0m)
            throw new InvalidOperationException("Starting bankroll must be positive.");
    }
}
