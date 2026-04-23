namespace PlusEV.Core.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Alerts:Telegram";
    public bool Enabled { get; set; }
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
    public double MinEvPercent { get; set; } = 0.03d;
    public List<string> SportKeys { get; set; } = new();
}
