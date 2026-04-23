using Microsoft.EntityFrameworkCore;
using PlusEV.Core.Domain;
using PlusEV.Core.Math;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.Application.Services;

/// <summary>
/// Builds the Reality Check dashboard snapshot by streaming CLV values out of the Bets
/// table into <see cref="ClvStatistics"/>.
/// </summary>
public sealed class RealityCheckService
{
    private readonly PlusEvDbContext _db;
    public RealityCheckService(PlusEvDbContext db) => _db = db;

    public async Task<RealityCheckSnapshot> GetAsync(Mode mode, CancellationToken ct = default)
    {
        var stats = new ClvStatistics();
        var convergence = new List<(int N, double Mean)>();

        var clvs = _db.Bets.AsNoTracking()
            .Where(b => b.Mode == mode && b.Clv.HasValue)
            .OrderBy(b => b.PlacedAt)
            .Select(b => b.Clv!.Value);

        await foreach (var clv in clvs.AsAsyncEnumerable().WithCancellation(ct))
        {
            stats.Push(clv);
            if (stats.Count % 25 == 0 || stats.Count < 25) convergence.Add((stats.Count, stats.Mean));
        }

        return new RealityCheckSnapshot
        {
            Mode = mode,
            SampleSize = stats.Count,
            MeanClv = stats.Mean,
            StdDev = stats.StdDev,
            TStatistic = stats.TStatistic,
            PValue = stats.PValue,
            RequiredSampleSize = stats.RequiredSampleSize(),
            Verdict = stats.Verdict(),
            Convergence = convergence,
        };
    }
}

public sealed class RealityCheckSnapshot
{
    public Mode Mode { get; init; }
    public int SampleSize { get; init; }
    public double MeanClv { get; init; }
    public double StdDev { get; init; }
    public double TStatistic { get; init; }
    public double PValue { get; init; }
    public int RequiredSampleSize { get; init; }
    public RealityVerdict Verdict { get; init; }
    public IReadOnlyList<(int N, double Mean)> Convergence { get; init; } = Array.Empty<(int, double)>();
}
