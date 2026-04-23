using PlusEV.Core.Domain;

namespace PlusEV.Core.Backtesting;

/// <summary>Summary statistics for one half of a backtest (in-sample OR out-of-sample).</summary>
public sealed class BacktestMetrics
{
    public int TotalBets { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }
    public int Pushes { get; init; }
    public int Voids { get; init; }
    public decimal TotalStaked { get; init; }
    public decimal Profit { get; init; }
    public decimal EndingBankroll { get; init; }
    public decimal MaxDrawdown { get; init; }
    public double WinRate { get; init; }
    public double RoiPercent { get; init; }
    public double AverageClv { get; init; }
    public double SharpeRatio { get; init; }
    public int LongestLosingStreak { get; init; }

    /// <summary>Bankroll after each settled bet (in order).</summary>
    public IReadOnlyList<BankrollPoint> BankrollCurve { get; init; } = Array.Empty<BankrollPoint>();

    /// <summary>Simulated bets for the detail grid.</summary>
    public IReadOnlyList<Bet> Bets { get; init; } = Array.Empty<Bet>();
}

public sealed record BankrollPoint(DateTimeOffset At, decimal Balance);

/// <summary>A complete walk-forward result: in-sample AND out-of-sample.</summary>
public sealed class BacktestReport
{
    public required BacktestConfig Config { get; init; }
    public required BacktestMetrics InSample { get; init; }
    public required BacktestMetrics OutOfSample { get; init; }
    public required DateTimeOffset RanAt { get; init; }
    public required string RunId { get; init; }

    /// <summary>
    /// Degree to which in-sample ROI outperforms out-of-sample ROI (percentage points).
    /// A large positive value is an overfitting red flag.
    /// </summary>
    public double OverfittingGap => InSample.RoiPercent - OutOfSample.RoiPercent;

    public bool OverfittingWarning => OverfittingGap > 5d;
}
