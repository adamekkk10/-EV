using Refit;

namespace PlusEV.Infrastructure.Odds;

/// <summary>
/// Refit interface over The Odds API v4.
/// Docs: https://the-odds-api.com/liveapi/guides/v4/
/// </summary>
public interface ITheOddsApiClient
{
    [Get("/sports")]
    Task<ApiResponse<List<TheOddsApiSportDto>>> GetSportsAsync(
        [AliasAs("apiKey")] string apiKey,
        CancellationToken ct = default);

    [Get("/sports/{sport}/odds")]
    Task<ApiResponse<List<TheOddsApiEventDto>>> GetOddsAsync(
        string sport,
        [AliasAs("apiKey")] string apiKey,
        [AliasAs("regions")] string regions,
        [AliasAs("markets")] string markets,
        [AliasAs("oddsFormat")] string oddsFormat,
        [AliasAs("bookmakers")] string? bookmakers = null,
        CancellationToken ct = default);

    [Get("/historical/sports/{sport}/odds")]
    Task<ApiResponse<TheOddsApiHistoricalResponseDto>> GetHistoricalOddsAsync(
        string sport,
        [AliasAs("apiKey")] string apiKey,
        [AliasAs("regions")] string regions,
        [AliasAs("markets")] string markets,
        [AliasAs("oddsFormat")] string oddsFormat,
        [AliasAs("date")] string iso8601Date,
        CancellationToken ct = default);

    [Get("/sports/{sport}/scores")]
    Task<ApiResponse<List<TheOddsApiScoreDto>>> GetScoresAsync(
        string sport,
        [AliasAs("apiKey")] string apiKey,
        [AliasAs("daysFrom")] int daysFrom,
        CancellationToken ct = default);
}
