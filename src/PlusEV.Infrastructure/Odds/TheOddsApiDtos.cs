using System.Text.Json.Serialization;

namespace PlusEV.Infrastructure.Odds;

/// <summary>DTOs matching The Odds API v4 JSON payloads.</summary>
public sealed class TheOddsApiSportDto
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("group")] public string? Group { get; set; }
    [JsonPropertyName("active")] public bool Active { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public sealed class TheOddsApiEventDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("sport_key")] public string SportKey { get; set; } = "";
    [JsonPropertyName("sport_title")] public string SportTitle { get; set; } = "";
    [JsonPropertyName("commence_time")] public DateTimeOffset CommenceTime { get; set; }
    [JsonPropertyName("home_team")] public string HomeTeam { get; set; } = "";
    [JsonPropertyName("away_team")] public string AwayTeam { get; set; } = "";
    [JsonPropertyName("bookmakers")] public List<TheOddsApiBookmakerDto> Bookmakers { get; set; } = new();
}

public sealed class TheOddsApiBookmakerDto
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("last_update")] public DateTimeOffset LastUpdate { get; set; }
    [JsonPropertyName("markets")] public List<TheOddsApiMarketDto> Markets { get; set; } = new();
}

public sealed class TheOddsApiMarketDto
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("last_update")] public DateTimeOffset LastUpdate { get; set; }
    [JsonPropertyName("outcomes")] public List<TheOddsApiOutcomeDto> Outcomes { get; set; } = new();
}

public sealed class TheOddsApiOutcomeDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("price")] public decimal Price { get; set; }
    [JsonPropertyName("point")] public double? Point { get; set; }
}

public sealed class TheOddsApiHistoricalResponseDto
{
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }
    [JsonPropertyName("previous_timestamp")] public DateTimeOffset? PreviousTimestamp { get; set; }
    [JsonPropertyName("next_timestamp")] public DateTimeOffset? NextTimestamp { get; set; }
    [JsonPropertyName("data")] public List<TheOddsApiEventDto> Data { get; set; } = new();
}

public sealed class TheOddsApiScoreDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("completed")] public bool Completed { get; set; }
    [JsonPropertyName("last_update")] public DateTimeOffset? LastUpdate { get; set; }
    [JsonPropertyName("home_team")] public string HomeTeam { get; set; } = "";
    [JsonPropertyName("away_team")] public string AwayTeam { get; set; } = "";
    [JsonPropertyName("scores")] public List<TheOddsApiScoreEntry>? Scores { get; set; }
}

public sealed class TheOddsApiScoreEntry
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("score")] public string Score { get; set; } = "0";
}
