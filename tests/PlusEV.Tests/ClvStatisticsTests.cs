using FluentAssertions;
using PlusEV.Core.Math;
using Xunit;

namespace PlusEV.Tests;

public class ClvStatisticsTests
{
    [Fact]
    public void Insufficient_data_verdict_before_50_samples()
    {
        var s = new ClvStatistics();
        for (int i = 0; i < 30; i++) s.Push(0.02d);
        s.Verdict().Should().Be(RealityVerdict.InsufficientData);
    }

    [Fact]
    public void Significant_positive_clv_produces_edge_confirmed()
    {
        var s = new ClvStatistics();
        // 600 samples at a 2% edge with low noise.
        var rng = new Random(42);
        for (int i = 0; i < 600; i++) s.Push(0.02d + (rng.NextDouble() - 0.5d) * 0.01d);
        s.Verdict().Should().BeOneOf(RealityVerdict.EdgeConfirmed, RealityVerdict.EdgeEmerging);
        s.TStatistic.Should().BeGreaterThan(2d);
    }

    [Fact]
    public void Negative_clv_detected()
    {
        var s = new ClvStatistics();
        var rng = new Random(7);
        for (int i = 0; i < 300; i++) s.Push(-0.03d + (rng.NextDouble() - 0.5d) * 0.01d);
        s.Verdict().Should().Be(RealityVerdict.NegativeEdgeDetected);
    }

    [Fact]
    public void Mean_and_stddev_computed_incrementally()
    {
        var s = new ClvStatistics();
        var values = new[] { 0.01d, 0.02d, 0.03d, 0.04d, 0.05d };
        foreach (var v in values) s.Push(v);
        s.Mean.Should().BeApproximately(0.03d, 1e-9);
        s.StdDev.Should().BeApproximately(0.015811d, 1e-5);
    }
}
