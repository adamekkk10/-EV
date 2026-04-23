using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Domain;
using PlusEV.Core.Options;

namespace PlusEV.Infrastructure.Odds;

/// <summary>Event-result provider backed by The Odds API scores endpoint.</summary>
public sealed class TheOddsApiResultProvider : IEventResultProvider
{
    private readonly ITheOddsApiClient _client;
    private readonly OddsApiOptions _options;
    private readonly ILogger<TheOddsApiResultProvider> _logger;

    public TheOddsApiResultProvider(
        ITheOddsApiClient client,
        IOptions<OddsApiOptions> options,
        ILogger<TheOddsApiResultProvider> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EventResult>> GetResultsAsync(
        Sport sport,
        DateTimeOffset since,
        CancellationToken ct = default)
    {
        var daysFrom = System.Math.Clamp((int)(DateTimeOffset.UtcNow - since).TotalDays + 1, 1, 3);
        var response = await _client.GetScoresAsync(sport.Key, _options.ApiKey, daysFrom, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var list = new List<EventResult>(response.Content?.Count ?? 0);
        foreach (var s in response.Content ?? new())
        {
            int? homeScore = null, awayScore = null;
            if (s.Scores is not null)
            {
                homeScore = FindScore(s.Scores, s.HomeTeam);
                awayScore = FindScore(s.Scores, s.AwayTeam);
            }
            list.Add(new EventResult(
                s.Id,
                s.Completed,
                homeScore,
                awayScore,
                s.LastUpdate ?? DateTimeOffset.UtcNow));
        }
        return list;
    }

    private static int? FindScore(List<TheOddsApiScoreEntry> scores, string team)
    {
        var entry = scores.FirstOrDefault(s => string.Equals(s.Name, team, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        return int.TryParse(entry.Score, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
}
