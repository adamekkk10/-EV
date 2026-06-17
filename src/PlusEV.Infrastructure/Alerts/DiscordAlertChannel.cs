using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Options;

namespace PlusEV.Infrastructure.Alerts;

/// <summary>Sends alerts to a Discord channel via incoming webhook.</summary>
public sealed class DiscordAlertChannel : IAlertChannel
{
    private readonly DiscordOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<DiscordAlertChannel> _logger;

    public DiscordAlertChannel(
        IOptions<DiscordOptions> options,
        IHttpClientFactory factory,
        ILogger<DiscordAlertChannel> logger)
    {
        _options = options.Value;
        _http = factory.CreateClient(nameof(DiscordAlertChannel));
        _logger = logger;
    }

    public string Key => "discord";
    public string DisplayName => "Discord";
    public bool Enabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.WebhookUrl);

    public bool ShouldSend(AlertMessage _) => Enabled;

    public async Task SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        if (!Enabled) return;
        var colour = message.Severity switch
        {
            AlertSeverity.Error => 0xD4183D,
            AlertSeverity.Warning => 0xE6A23C,
            AlertSeverity.Success => 0x2BB673,
            _ => 0x4A90E2,
        };
        var payload = new
        {
            content = (string?)null,
            embeds = new[]
            {
                new
                {
                    title = message.Title,
                    description = message.Body,
                    color = colour,
                    timestamp = message.CreatedAt.ToString("o"),
                }
            }
        };
        using var response = await _http.PostAsJsonAsync(_options.WebhookUrl, payload, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("Discord webhook failed {Status}: {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }
    }

    public Task TestAsync(CancellationToken ct = default) =>
        SendAsync(new AlertMessage(
            "PlusEV test alert",
            "Discord webhook is configured correctly.",
            AlertSeverity.Info, AlertCategory.System, DateTimeOffset.UtcNow), ct);
}
