using FluentAssertions;
using PlusEV.Core.Math;
using Xunit;

namespace PlusEV.Tests;

public class KellyTests
{
    [Fact]
    public void Fraction_zero_when_edge_is_zero()
    {
        Kelly.Fraction(0.5d, 2.0d).Should().Be(0d);
    }

    [Fact]
    public void Fraction_positive_when_edge_is_positive()
    {
        // 55% at 2.0 => edge of 10%. Kelly = (1*0.55 - 0.45) / 1 = 0.10.
        Kelly.Fraction(0.55d, 2.0d).Should().BeApproximately(0.10d, 1e-9);
    }

    [Fact]
    public void Fraction_zero_for_negative_edge()
    {
        Kelly.Fraction(0.40d, 2.0d).Should().Be(0d);
    }

    [Fact]
    public void RecommendedStake_applies_quarter_kelly_and_hard_cap()
    {
        // Full Kelly 10%, quarter = 2.5%. Bankroll 1000 => 25. Hard cap 2% => 20.
        var stake = Kelly.RecommendedStake(1000m, 0.55d, 2.0d, kellyFraction: 0.25d, hardCapFractionOfBankroll: 0.02d);
        stake.Should().Be(20m);
    }

    [Fact]
    public void RecommendedStake_respects_soft_cap()
    {
        // Average stake 10, soft cap 5x => 50 max.
        var stake = Kelly.RecommendedStake(1000m, 0.80d, 3.0d, kellyFraction: 0.5d,
            hardCapFractionOfBankroll: 0.50d, rollingAverageStake: 10m, softCapMultiple: 5d);
        stake.Should().BeLessOrEqualTo(50m);
    }

    [Fact]
    public void FlatStake_returns_expected_amount()
    {
        Kelly.FlatStake(1000m, 0.01d).Should().Be(10m);
    }
}
