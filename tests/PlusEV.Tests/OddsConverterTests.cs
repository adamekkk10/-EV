using FluentAssertions;
using PlusEV.Core.Math;
using Xunit;

namespace PlusEV.Tests;

public class OddsConverterTests
{
    [Theory]
    [InlineData(-110, 1.9091)]
    [InlineData(+100, 2.0000)]
    [InlineData(+150, 2.5000)]
    [InlineData(-200, 1.5000)]
    [InlineData(+250, 3.5000)]
    public void AmericanToDecimal_matches_standard_conversion(int american, double expected)
    {
        ((double)OddsConverter.AmericanToDecimal(american)).Should().BeApproximately(expected, 0.0005);
    }

    [Theory]
    [InlineData(1.91m, -110)]
    [InlineData(2.00m, 100)]
    [InlineData(2.50m, 150)]
    [InlineData(1.50m, -200)]
    public void DecimalToAmerican_round_trips(decimal dec, int expected)
    {
        OddsConverter.DecimalToAmerican(dec).Should().Be(expected);
    }

    [Fact]
    public void ProbabilityToDecimal_is_reciprocal()
    {
        OddsConverter.ProbabilityToDecimal(0.5m).Should().Be(2m);
        OddsConverter.ProbabilityToDecimal(0.25m).Should().Be(4m);
    }

    [Theory]
    [InlineData("+150", 150)]
    [InlineData("-110", -110)]
    [InlineData("EVEN", 100)]
    [InlineData("PK", 100)]
    public void ParseAmerican_reads_common_formats(string text, int expected)
    {
        OddsConverter.ParseAmerican(text).Should().Be(expected);
    }

    [Fact]
    public void AmericanToDecimal_rejects_zero()
    {
        var act = () => OddsConverter.AmericanToDecimal(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
