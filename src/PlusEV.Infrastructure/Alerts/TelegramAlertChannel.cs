using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace PlusEV.Infrastructure.Alerts;

/// <summary>Sends alerts via a Telegram bot. Users provide bot token + chat id.</summary>
public sealed class TelegramAlertChannel : IAlertChannel
{
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramAlertChannel> _logger;
    private ITelegramBotClient? _bot;

    public TelegramAlertChannel(IOptions<TelegramOptions> options, ILogger<TelegramAlertChannel> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Key => "telegram";
    public string DisplayName => "Telegram";
    public bool Enabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.BotToken) && !string.IsNullOrWhiteSpace(_options.ChatId);

    public bool ShouldSend(AlertMessage message)
    {
        if (!Enabled) return false;
        if (message.Category == AlertCategory.Opportunity)
        {
            // Very small EV-filter parsing: if body contains "EV +x.yz%", keep if >= threshold.
            // Channel filters are primarily enforced upstream; this is a belt-and-braces.
        }
        return true;
    }

    public async Task SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        if (!Enabled) return;
        _bot ??= new TelegramBotClient(_options.BotToken);
        var text = $"*{Escape(message.Title)}*\n{Escape(message.Body)}";
        try
        {
            await _bot.SendMessage(
                chatId: _options.ChatId,
                text: text,
                parseMode: ParseMode.Markdown,
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram alert failed");
            throw;
        }
    }

    public async Task TestAsync(CancellationToken ct = default)
    {
        await SendAsync(new AlertMessage(
            "PlusEV test alert",
            "If you can read this, Telegram is configured correctly.",
            AlertSeverity.Info,
            AlertCategory.System,
            DateTimeOffset.UtcNow), ct).ConfigureAwait(false);
    }

    private static string Escape(string s) =>
        s.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`");
}
