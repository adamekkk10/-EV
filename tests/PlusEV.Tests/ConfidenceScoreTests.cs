using FluentAssertions;
using PlusEV.Core.Math;
using Xunit;

namespace PlusEV.Tests;

public class ConfidenceScoreTests
{
    [Fact]
    public void Score_increases_with_ev_magnitude()
    {
        var low = ConfidenceScore.Compute(new ConfidenceInputs
        {
            EvPercent = 0.02d, MarketDepth = 8, PinnacleOverround = 1.04d,
            HoursToEvent = 12, LineStabilityStdDev = 0.005d,
        });
        var high = ConfidenceScore.Compute(new ConfidenceInputs
        {
            EvPercent = 0.08d, MarketDepth = 8, PinnacleOverround = 1.04d,
            HoursToEvent = 12, LineStabilityStdDev = 0.005d,
        });
        high.Should().BeGreaterThan(low);
    }

    [Fact]
    public void Score_is_in_0_100()
    {
        var score = ConfidenceScore.Compute(new ConfidenceInputs
        {
            EvPercent = 0.1d, MarketDepth = 20, PinnacleOverround = 1.02d,
            HoursToEvent = 6, LineStabilityStdDev = 0d,
        });
        score.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(100);
    }

    [Fact]
    public void Score_penalises_stale_or_far_out_events()
    {
        var close = ConfidenceScore.Compute(new ConfidenceInputs
        {
            EvPercent = 0.04d, MarketDepth = 6, PinnacleOverround = 1.04d,
            HoursToEvent = 8, LineStabilityStdDev = 0.005d,
        });
        var far = ConfidenceScore.Compute(new ConfidenceInputs
        {
            EvPercent = 0.04d, MarketDepth = 6, PinnacleOverround = 1.04d,
            HoursToEvent = 240, LineStabilityStdDev = 0.005d,
        });
        close.Should().BeGreaterThan(far);
    }
}
