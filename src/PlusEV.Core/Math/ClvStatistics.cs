using MathNet.Numerics.Distributions;

namespace PlusEV.Core.Math;

/// <summary>
/// Running statistics on Closing Line Value. CLV is the only metric that stabilises
/// before P/L does, so it's the primary KPI on the Reality Check dashboard.
/// </summary>
public sealed class ClvStatistics
{
    /// <summary>Number of samples observed.</summary>
    public int Count { get; private set; }

    /// <summary>Sample mean.</summary>
    public double Mean { get; private set; }

    /// <summary>Sum of squared deviations (Welford accumulator).</summary>
    private double _m2;

    /// <summary>Sample variance (n-1 denominator); 0 when <see cref="Count"/>&lt;2.</summary>
    public double Variance => Count < 2 ? 0d : _m2 / (Count - 1);

    /// <summary>Sample standard deviation.</summary>
    public double StdDev => System.Math.Sqrt(Variance);

    /// <summary>Standard error of the mean.</summary>
    public double StandardError => Count < 2 ? 0d : StdDev / System.Math.Sqrt(Count);

    /// <summary>
    /// t-statistic for the null hypothesis μ=0 (no edge). Positive values mean the
    /// observed CLV is above zero; &gt;2 is the conventional "significant" threshold.
    /// </summary>
    public double TStatistic => Count < 2 || StandardError == 0d ? 0d : Mean / StandardError;

    /// <summary>
    /// Two-sided p-value under a Student-t with n-1 df. Uses Math.NET StudentT.
    /// </summary>
    public double PValue
    {
        get
        {
            if (Count < 2) return 1d;
            var df = Count - 1;
            var t = TStatistic;
            var cdf = StudentT.CDF(0d, 1d, df, System.Math.Abs(t));
            return 2d * (1d - cdf);
        }
    }

    /// <summary>Feed one sample (e.g. a bet's CLV like 0.023 = +2.3%).</summary>
    public void Push(double clv)
    {
        Count++;
        var delta = clv - Mean;
        Mean += delta / Count;
        var delta2 = clv - Mean;
        _m2 += delta * delta2;
    }

    /// <summary>
    /// Approximate sample-size needed to declare the current mean "significant" at α=0.05
    /// (two-sided), assuming the observed variance persists. Returns int.MaxValue if
    /// we have no usable estimate yet.
    /// </summary>
    public int RequiredSampleSize(double alpha = 0.05d)
    {
        if (Count < 10 || Mean <= 0d || Variance <= 0d) return int.MaxValue;
        var zCrit = Normal.InvCDF(0d, 1d, 1d - alpha / 2d);
        var needed = (zCrit * zCrit * Variance) / (Mean * Mean);
        var n = (int)System.Math.Ceiling(needed);
        return n < 30 ? 30 : n; // never claim fewer than 30.
    }

    public RealityVerdict Verdict(int verdictThreshold = 500)
    {
        if (Count < 50) return RealityVerdict.InsufficientData;
        if (Mean < 0d && TStatistic < -2d) return RealityVerdict.NegativeEdgeDetected;
        if (Count < verdictThreshold)
        {
            if (TStatistic > 2d) return RealityVerdict.EdgeEmerging;
            return RealityVerdict.EdgeNotSignificant;
        }
        if (TStatistic > 2.5d && Mean > 0d) return RealityVerdict.EdgeConfirmed;
        if (TStatistic > 1.5d) return RealityVerdict.EdgeEmerging;
        return RealityVerdict.EdgeNotSignificant;
    }
}

/// <summary>High-level verdicts shown on the Reality Check dashboard.</summary>
public enum RealityVerdict
{
    InsufficientData,
    EdgeNotSignificant,
    EdgeEmerging,
    EdgeConfirmed,
    NegativeEdgeDetected,
}
