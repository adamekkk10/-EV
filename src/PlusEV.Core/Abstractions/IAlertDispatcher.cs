using PlusEV.Core.Domain;

namespace PlusEV.Core.Abstractions;

/// <summary>
/// Generic alert notification with channel-routing hints.
/// </summary>
public sealed record AlertMessage(
    string Title,
    string Body,
    AlertSeverity Severity,
    AlertCategory Category,
    DateTimeOffset CreatedAt);

public enum AlertSeverity { Info, Success, Warning, Error }
public enum AlertCategory { Opportunity, Arbitrage, Middle, BotAction, DailySummary, System }

/// <summary>A single alert channel (desktop, Telegram, Discord, ...).</summary>
public interface IAlertChannel
{
    string Key { get; }
    string DisplayName { get; }
    bool Enabled { get; }

    /// <summary>Per-channel filter: is this alert allowed to reach the channel?</summary>
    bool ShouldSend(AlertMessage message);

    Task SendAsync(AlertMessage message, CancellationToken ct = default);

    /// <summary>Send a probe message the user can use to verify setup.</summary>
    Task TestAsync(CancellationToken ct = default);
}

/// <summary>
/// Fan-out dispatcher that queues messages and delivers to all enabled channels
/// in the background so notification latency can never block the engine.
/// </summary>
public interface IAlertDispatcher
{
    Task EnqueueAsync(AlertMessage message, CancellationToken ct = default);

    /// <summary>Build a message from an EV opportunity.</summary>
    AlertMessage FromOpportunity(EvOpportunity opportunity);
    AlertMessage FromArbitrage(ArbitrageOpportunity arb);
}
