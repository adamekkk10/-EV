namespace PlusEV.Core.Domain;

/// <summary>A persisted record of every alert we tried to send (for the Alerts view log).</summary>
public sealed class AlertLogEntry
{
    public long Id { get; set; }
    public required string Channel { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public required string Severity { get; set; }
    public required string Category { get; set; }
    public bool Delivered { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
