namespace PlusEV.Core.Options;

public enum DemoBotAggressiveness { Conservative, Balanced, Aggressive }

/// <summary>Runtime config for the demo "auto-betting" bot.</summary>
public sealed class DemoBotOptions
{
    public const string SectionName = "DemoBot";

    public bool Autostart { get; set; } = false;
    public DemoBotAggressiveness Aggressiveness { get; set; } = DemoBotAggressiveness.Balanced;

    /// <summary>EV threshold overridden by the aggressiveness preset if the UI hasn't pinned it.</summary>
    public double MinEvPercent { get; set; } = 0.025d;

    /// <summary>Max simultaneous open virtual positions.</summary>
    public int MaxOpenPositions { get; set; } = 25;

    /// <summary>Sport keys the bot is allowed to bet.</summary>
    public List<string> SportKeys { get; set; } = new();

    /// <summary>Book keys the bot is allowed to bet at.</summary>
    public List<string> BookKeys { get; set; } = new();

    public double KellyFraction { get; set; } = 0.25d;
    public double HardCapFractionOfBankroll { get; set; } = 0.02d;

    public static (double MinEv, double KellyFraction, double HardCap) PresetFor(DemoBotAggressiveness a) => a switch
    {
        DemoBotAggressiveness.Conservative => (0.04d, 0.15d, 0.01d),
        DemoBotAggressiveness.Balanced => (0.025d, 0.25d, 0.02d),
        DemoBotAggressiveness.Aggressive => (0.015d, 0.5d, 0.03d),
        _ => (0.025d, 0.25d, 0.02d),
    };
}
