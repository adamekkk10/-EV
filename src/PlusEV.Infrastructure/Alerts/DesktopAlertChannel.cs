using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Options;

namespace PlusEV.Infrastructure.Alerts;

/// <summary>
/// Platform-abstracted desktop notification channel. The UI layer injects a concrete
/// <see cref="IDesktopNotifier"/> that wraps platform APIs (ToastNotificationManager on
/// Windows, <c>osascript</c> on macOS, <c>notify-send</c> on Linux).
/// </summary>
public sealed class DesktopAlertChannel : IAlertChannel
{
    private readonly IDesktopNotifier _notifier;
    private readonly DesktopAlertOptions _options;
    private readonly ILogger<DesktopAlertChannel> _logger;

    public DesktopAlertChannel(
        IDesktopNotifier notifier,
        IOptions<DesktopAlertOptions> options,
        ILogger<DesktopAlertChannel> logger)
    {
        _notifier = notifier;
        _options = options.Value;
        _logger = logger;
    }

    public string Key => "desktop";
    public string DisplayName => "Desktop notifications";
    public bool Enabled => _options.Enabled;

    public bool ShouldSend(AlertMessage _) => Enabled;

    public Task SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        if (!Enabled) return Task.CompletedTask;
        try { _notifier.Notify(message.Title, message.Body); }
        catch (Exception ex) { _logger.LogWarning(ex, "Desktop notification failed"); throw; }
        return Task.CompletedTask;
    }

    public Task TestAsync(CancellationToken ct = default) =>
        SendAsync(new AlertMessage(
            "PlusEV test alert",
            "Desktop notifications are working.",
            AlertSeverity.Info, AlertCategory.System, DateTimeOffset.UtcNow), ct);
}

/// <summary>Platform-specific notification implementation (provided by the UI layer).</summary>
public interface IDesktopNotifier
{
    void Notify(string title, string body);
}

/// <summary>Default no-op notifier used when the UI didn't register one (e.g. headless tests).</summary>
public sealed class NullDesktopNotifier : IDesktopNotifier
{
    public void Notify(string title, string body) { /* no-op */ }
}
