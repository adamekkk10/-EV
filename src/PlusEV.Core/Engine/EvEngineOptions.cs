namespace PlusEV.Core.Engine;

/// <summary>
/// Runtime knobs for the EV engine. Bind from appsettings via <c>IOptions&lt;EvEngineOptions&gt;</c>.
/// </summary>
public sealed class EvEngineOptions
{
    public const string SectionName = "Engine";

    /// <summary>Minimum EV% to surface an opportunity (default +2%).</summary>
    public double MinEvPercent { get; set; } = 0.02d;

    /// <summary>Minimum confidence to surface an opportunity (0–100). Default 40.</summary>
    public double MinConfidence { get; set; } = 40d;

    /// <summary>Max absolute difference between Pinnacle's devigged probability and the
    /// market consensus before we flag the data as stale and reject the opportunity.
    /// Default 5 percentage points.</summary>
    public double SanityCheckMaxDeltaProbability { get; set; } = 0.05d;

    /// <summary>Devigging method key (matches <c>DevigMethods.All</c>). Default "power".</summary>
    public string DevigMethodKey { get; set; } = "power";

    /// <summary>Kelly fraction (0.25 = quarter Kelly).</summary>
    public double KellyFraction { get; set; } = 0.25d;

    /// <summary>Hard cap as fraction of bankroll (0.02 = 2%).</summary>
    public double HardCapFractionOfBankroll { get; set; } = 0.02d;

    /// <summary>Soft cap as a multiple of the rolling average stake.</summary>
    public double SoftCapMultipleOfAverage { get; set; } = 5d;
}
