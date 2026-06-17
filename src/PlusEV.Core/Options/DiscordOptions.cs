namespace PlusEV.Core.Options;

public sealed class DiscordOptions
{
    public const string SectionName = "Alerts:Discord";
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = "";
    public double MinEvPercent { get; set; } = 0.03d;
    public List<string> SportKeys { get; set; } = new();
}
