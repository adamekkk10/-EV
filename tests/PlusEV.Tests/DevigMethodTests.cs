using FluentAssertions;
using PlusEV.Core.Math;
using Xunit;

namespace PlusEV.Tests;

public class DevigMethodTests
{
    // -110 both sides => raw implied probs [0.5238, 0.5238], sum 1.0476.
    private static readonly double[] TwoWay = { 1d / 1.9091d, 1d / 1.9091d };

    // Three-way (draw market): 2.40 / 3.40 / 3.10 – overround ~1.0543.
    private static readonly double[] ThreeWay = { 1d / 2.40d, 1d / 3.40d, 1d / 3.10d };

    [Fact]
    public void Multiplicative_two_way_symmetric()
    {
        var result = new MultiplicativeDevig().Devig(TwoWay);
        result.Should().HaveCount(2);
        result[0].Should().BeApproximately(0.5d, 1e-9);
        result[1].Should().BeApproximately(0.5d, 1e-9);
        (result[0] + result[1]).Should().BeApproximately(1d, 1e-9);
    }

    [Fact]
    public void Power_sums_to_one()
    {
        var result = new PowerDevig().Devig(TwoWay);
        (result[0] + result[1]).Should().BeApproximately(1d, 1e-9);
    }

    [Fact]
    public void Shin_two_way_sums_to_one()
    {
        var result = new ShinDevig().Devig(TwoWay);
        (result[0] + result[1]).Should().BeApproximately(1d, 1e-9);
    }

    [Fact]
    public void Power_and_multiplicative_agree_on_symmetric_two_way()
    {
        var mult = new MultiplicativeDevig().Devig(TwoWay);
        var pow = new PowerDevig().Devig(TwoWay);
        // Symmetric input => both methods should place 0.5 on each side.
        mult[0].Should().BeApproximately(pow[0], 1e-6);
    }

    [Fact]
    public void Power_recovers_proportional_sum_on_three_way()
    {
        var result = new PowerDevig().Devig(ThreeWay);
        result.Sum().Should().BeApproximately(1d, 1e-9);
        // Each fair prob less than the raw implied prob (we removed vig).
        for (int i = 0; i < ThreeWay.Length; i++)
            result[i].Should().BeLessOrEqualTo(ThreeWay[i]);
    }

    [Fact]
    public void All_methods_handle_vig_free_market_as_identity()
    {
        var fair = new double[] { 0.5d, 0.5d };
        foreach (var m in DevigMethods.All)
        {
            var r = m.Devig(fair);
            r[0].Should().BeApproximately(0.5d, 1e-8);
            r[1].Should().BeApproximately(0.5d, 1e-8);
        }
    }

    [Fact]
    public void Power_on_asymmetric_two_way_removes_more_vig_from_longshot()
    {
        // 70/30 style market with 4.8% vig: implied probs 0.7333, 0.3143 => sum ~1.0476.
        var raw = new double[] { 1d / 1.3636d, 1d / 3.1818d };
        var mult = new MultiplicativeDevig().Devig(raw);
        var pow = new PowerDevig().Devig(raw);
        // Power devig should produce the longshot with lower probability than
        // proportional devig (it removes more vig from longshots).
        pow[1].Should().BeLessThan(mult[1]);
    }
}
