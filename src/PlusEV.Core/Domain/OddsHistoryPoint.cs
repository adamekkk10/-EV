namespace PlusEV.Core.Domain;

/// <summary>
/// A single stored line snapshot, one row per outcome per book per fetch. The background
/// ingestion service writes these and the Line History view reads them back.
/// </summary>
public sealed class OddsHistoryPoint
{
    public long Id { get; set; }
    public required Mode Mode { get; set; }
    public required string EventId { get; set; }
    public required string Sport { get; set; }
    public required MarketType Market { get; set; }
    public required string BookKey { get; set; }
    public required string Selection { get; set; }
    public double? Point { get; set; }
    public required decimal DecimalOdds { get; set; }
    public DateTimeOffset BookUpdatedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>Latency from book's last update to our fetch (ms).</summary>
    public long FetchLatencyMs { get; set; }
}
