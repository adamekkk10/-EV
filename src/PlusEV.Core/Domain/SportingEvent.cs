namespace PlusEV.Core.Domain;

/// <summary>
/// A concrete event we might bet on (a game, match, bout, etc.).
/// Identified by the upstream provider's event id so snapshots from
/// different fetches can be correlated.
/// </summary>
public sealed class SportingEvent
{
    public required string Id { get; init; }
    public required Sport Sport { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public required DateTimeOffset Commence { get; init; }

    /// <summary>Human label e.g. "Lakers @ Celtics".</summary>
    public string Matchup => $"{AwayTeam} @ {HomeTeam}";
}
