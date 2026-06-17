namespace PlusEV.Core.Math;

/// <summary>
/// Inputs used to compute a 0–100 confidence score for an opportunity.
/// All ranges are documented per field; out-of-range values are clamped.
/// </summary>
public sealed record ConfidenceInputs
{
    /// <summary>Raw EV (e.g. 0.04 for +4%).</summary>
    public double EvPercent { get; init; }

    /// <summary>Number of books offering a price on this market.</summary>
    public int MarketDepth { get; init; }

    /// <summary>Pinnacle's overround (e.g. 1.045 for 4.5% vig). Lower is better.</summary>
    public double PinnacleOverround { get; init; } = 1.05d;

    /// <summary>Hours until event commences. Shorter is higher quality.</summary>
    public double HoursToEvent { get; init; }

    /// <summary>Standard deviation of the sharp books' devigged probabilities for this selection,
    /// as a fraction (e.g. 0.01 = 1%). Lower (more agreement) is better.</summary>
    public double LineStabilityStdDev { get; init; }
}

/// <summary>
/// Produces a 0–100 confidence score blending EV magnitude, market depth, Pinnacle vig
/// tightness, time-to-event and line stability. Weights chosen to match intuition on
/// the typical spec: good +EV at a deep market close to kickoff with tight sharp vig =&gt; high score.
/// </summary>
public static class ConfidenceScore
{
    public static double Compute(ConfidenceInputs inputs)
    {
        // Each component is normalised to [0,1].
        var ev = Saturate(inputs.EvPercent / 0.10d);                    // 10% EV saturates.
        var depth = Saturate((inputs.MarketDepth - 2d) / 8d);           // 10+ books saturates.
        var vig = Saturate((0.06d - (inputs.PinnacleOverround - 1d)) / 0.04d); // <2% vig saturates.
        var time = TimeCurve(inputs.HoursToEvent);
        var stability = Saturate(1d - inputs.LineStabilityStdDev / 0.03d); // <0% perfect, 3%+ worst.

        // Weights sum to 1.  EV dominates but never to the exclusion of the others.
        var score = 0.45d * ev
                  + 0.15d * depth
                  + 0.15d * vig
                  + 0.10d * time
                  + 0.15d * stability;

        return System.Math.Round(100d * Saturate(score), 1);
    }

    /// <summary>Prefers 1–24h window; penalises very early lines (soft) and in-play (hard).</summary>
    private static double TimeCurve(double hours)
    {
        if (hours <= 0d) return 0.1d;           // post-kick or missing data.
        if (hours < 1d) return 0.75d;           // very close — sharp
        if (hours < 24d) return 1d;             // sweet spot
        if (hours < 72d) return 0.7d;           // a few days out
        if (hours < 168d) return 0.45d;         // a week
        return 0.25d;
    }

    private static double Saturate(double x) => x < 0d ? 0d : (x > 1d ? 1d : x);
}
