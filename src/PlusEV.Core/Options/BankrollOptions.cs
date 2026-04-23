namespace PlusEV.Core.Options;

public sealed class BankrollOptions
{
    public const string SectionName = "Bankroll";
    public decimal LiveStarting { get; set; } = 1000m;
    public decimal DemoStarting { get; set; } = 1000m;

    /// <summary>Flat-stake fraction of bankroll if flat mode is enabled instead of Kelly.</summary>
    public double FlatStakeFractionOfBankroll { get; set; } = 0.01d;
    public bool UseFlatStaking { get; set; } = false;
}
