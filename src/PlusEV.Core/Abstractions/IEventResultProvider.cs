using PlusEV.Core.Domain;

namespace PlusEV.Core.Abstractions;

/// <summary>
/// Provides final scores / outcomes for settling bets (used by both the demo bot's
/// automatic settlement and the backtester).
/// </summary>
public interface IEventResultProvider
{
    /// <summary>Get settled results for events that have finished since <paramref name="since"/>.</summary>
    Task<IReadOnlyList<EventResult>> GetResultsAsync(
        Sport sport,
        DateTimeOffset since,
        CancellationToken ct = default);
}

/// <summary>Final state of an event sufficient to settle H2H / Spread / Total bets.</summary>
public sealed record EventResult(
    string EventId,
    bool Completed,
    int? HomeScore,
    int? AwayScore,
    DateTimeOffset FinishedAt);
