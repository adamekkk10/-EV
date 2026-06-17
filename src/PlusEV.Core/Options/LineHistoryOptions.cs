namespace PlusEV.Core.Options;

public sealed class LineHistoryOptions
{
    public const string SectionName = "LineHistory";
    public TimeSpan SnapshotInterval { get; set; } = TimeSpan.FromMinutes(5);
    public int SteamMoveMinBooks { get; set; } = 3;
    public double SteamMoveMinProbDelta { get; set; } = 0.02d;
    public TimeSpan SteamMoveWindow { get; set; } = TimeSpan.FromMinutes(5);
}
