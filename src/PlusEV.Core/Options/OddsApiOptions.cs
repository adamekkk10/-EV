namespace PlusEV.Core.Options;

/// <summary>Settings for The Odds API provider.</summary>
public sealed class OddsApiOptions
{
    public const string SectionName = "OddsApi";

    public string BaseUrl { get; set; } = "https://api.the-odds-api.com/v4";
    public string ApiKey { get; set; } = "";
    public string Region { get; set; } = "us";
    public string OddsFormat { get; set; } = "decimal";

    /// <summary>Poll interval for the live odds ingestion service.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Polly retry count for transient HTTP failures.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Polly circuit-breaker failure threshold.</summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromMinutes(1);
}
