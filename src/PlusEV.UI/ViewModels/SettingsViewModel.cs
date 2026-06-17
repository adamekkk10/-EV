using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using PlusEV.Core.Engine;
using PlusEV.Core.Options;

namespace PlusEV.UI.ViewModels;

/// <summary>
/// Bound form editor for the appsettings-derived options. Changes are held in memory on
/// the viewmodel and committed to a JSON overlay file when the user clicks Save.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(
        IOptions<OddsApiOptions> oddsApi,
        IOptions<EvEngineOptions> engine,
        IOptions<BankrollOptions> bankroll,
        IOptions<TelegramOptions> telegram,
        IOptions<DiscordOptions> discord,
        IOptions<DesktopAlertOptions> desktop)
    {
        ApiKey = oddsApi.Value.ApiKey;
        Region = oddsApi.Value.Region;
        PollIntervalSeconds = (int)oddsApi.Value.PollInterval.TotalSeconds;
        MinEvPercent = engine.Value.MinEvPercent * 100d;
        MinConfidence = engine.Value.MinConfidence;
        KellyFraction = engine.Value.KellyFraction;
        HardCap = engine.Value.HardCapFractionOfBankroll * 100d;
        SoftCapMultiple = engine.Value.SoftCapMultipleOfAverage;
        SanityDelta = engine.Value.SanityCheckMaxDeltaProbability * 100d;
        DevigMethodKey = engine.Value.DevigMethodKey;
        LiveStartingBankroll = bankroll.Value.LiveStarting;
        DemoStartingBankroll = bankroll.Value.DemoStarting;
        UseFlatStaking = bankroll.Value.UseFlatStaking;
        TelegramBotToken = telegram.Value.BotToken;
        TelegramChatId = telegram.Value.ChatId;
        DiscordWebhookUrl = discord.Value.WebhookUrl;
        DesktopEnabled = desktop.Value.Enabled;
    }

    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _region = "us";
    [ObservableProperty] private int _pollIntervalSeconds = 60;
    [ObservableProperty] private double _minEvPercent = 2d;
    [ObservableProperty] private double _minConfidence = 40d;
    [ObservableProperty] private double _kellyFraction = 0.25d;
    [ObservableProperty] private double _hardCap = 2d;
    [ObservableProperty] private double _softCapMultiple = 5d;
    [ObservableProperty] private double _sanityDelta = 5d;
    [ObservableProperty] private string _devigMethodKey = "power";
    [ObservableProperty] private decimal _liveStartingBankroll = 1000m;
    [ObservableProperty] private decimal _demoStartingBankroll = 1000m;
    [ObservableProperty] private bool _useFlatStaking;
    [ObservableProperty] private string _telegramBotToken = "";
    [ObservableProperty] private string _telegramChatId = "";
    [ObservableProperty] private string _discordWebhookUrl = "";
    [ObservableProperty] private bool _desktopEnabled = true;
    [ObservableProperty] private string _statusText = "";

    [RelayCommand]
    public async Task SaveAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.override.json");
        var payload = new
        {
            OddsApi = new
            {
                ApiKey,
                Region,
                PollInterval = TimeSpan.FromSeconds(PollIntervalSeconds).ToString(),
            },
            Engine = new
            {
                MinEvPercent = MinEvPercent / 100d,
                MinConfidence,
                KellyFraction,
                HardCapFractionOfBankroll = HardCap / 100d,
                SoftCapMultipleOfAverage = SoftCapMultiple,
                SanityCheckMaxDeltaProbability = SanityDelta / 100d,
                DevigMethodKey,
            },
            Bankroll = new { LiveStarting = LiveStartingBankroll, DemoStarting = DemoStartingBankroll, UseFlatStaking },
            Alerts = new
            {
                Telegram = new { BotToken = TelegramBotToken, ChatId = TelegramChatId },
                Discord = new { WebhookUrl = DiscordWebhookUrl },
                Desktop = new { Enabled = DesktopEnabled },
            },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        StatusText = $"Saved to {path}. Restart to apply.";
    }
}
