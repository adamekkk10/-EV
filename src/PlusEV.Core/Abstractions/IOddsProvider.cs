using PlusEV.Core.Domain;

namespace PlusEV.Core.Abstractions;

/// <summary>
/// Typed abstraction over an odds data provider. Allows swapping providers (The Odds API,
/// OddsJam, custom scraper, historical CSV) and mocking in tests.
/// </summary>
public interface IOddsProvider
{
    /// <summary>Stable key used in logs and settings (e.g. "the-odds-api").</summary>
    string Key { get; }

    /// <summary>Human label.</summary>
    string DisplayName { get; }

    /// <summary>Fetch the sports list available from this provider.</summary>
    Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetch current odds for a sport across the given markets and books.
    /// </summary>
    Task<IReadOnlyList<OddsSnapshot>> GetOddsAsync(
        Sport sport,
        IReadOnlyCollection<MarketType> markets,
        IReadOnlyCollection<string>? books = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch historical snapshots for the given sport at the given instant (provider permitting).
    /// Used by the backtester to build the replay timeline.
    /// </summary>
    Task<IReadOnlyList<OddsSnapshot>> GetHistoricalOddsAsync(
        Sport sport,
        DateTimeOffset at,
        IReadOnlyCollection<MarketType> markets,
        CancellationToken ct = default) =>
        throw new NotSupportedException($"{Key} does not support historical odds retrieval.");

    /// <summary>
    /// Remaining rate-limit budget if the provider surfaces it. Null if unknown.
    /// </summary>
    RateLimitInfo? LastKnownRateLimit { get; }
}

/// <summary>Rate limit counters reported by the provider, if any.</summary>
public sealed record RateLimitInfo(int Remaining, int Used, DateTimeOffset ObservedAt);
