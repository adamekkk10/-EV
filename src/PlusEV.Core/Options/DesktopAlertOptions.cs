namespace PlusEV.Core.Options;

public sealed class DesktopAlertOptions
{
    public const string SectionName = "Alerts:Desktop";
    public bool Enabled { get; set; } = true;
    public double MinEvPercent { get; set; } = 0.04d;
    public List<string> SportKeys { get; set; } = new();
}
