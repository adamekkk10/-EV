namespace PlusEV.Core.Math;

/// <summary>
/// Shin's (1992) method. Models the overround as arising from a fraction <c>z</c>
/// of insider traders. Solves for <c>z</c> such that the recovered probabilities sum to 1:
/// <c>p_i = (sqrt(z^2 + 4(1-z) * π_i^2 / Σπ) - z) / (2(1-z))</c> where π_i are raw implied
/// probabilities. Performs particularly well on two-way markets.
/// </summary>
public sealed class ShinDevig : IDevigMethod
{
    private const int MaxIterations = 200;
    private const double Tolerance = 1e-12;

    public string Key => "shin";
    public string DisplayName => "Shin (insider-trader model)";

    public double[] Devig(IReadOnlyList<double> rawImpliedProbabilities)
    {
        ArgumentNullException.ThrowIfNull(rawImpliedProbabilities);
        if (rawImpliedProbabilities.Count < 2)
            throw new ArgumentException("Shin requires at least 2 outcomes.", nameof(rawImpliedProbabilities));

        double sum = 0d;
        for (int i = 0; i < rawImpliedProbabilities.Count; i++)
        {
            var p = rawImpliedProbabilities[i];
            if (p <= 0d) throw new ArgumentException("Probabilities must be > 0.", nameof(rawImpliedProbabilities));
            sum += p;
        }

        // No vig => identity.
        if (System.Math.Abs(sum - 1d) < 1e-12)
        {
            var identity = new double[rawImpliedProbabilities.Count];
            for (int i = 0; i < identity.Length; i++) identity[i] = rawImpliedProbabilities[i];
            return identity;
        }

        // Bisection on z in [0, 1).  The constraint is that Σ ShinP(z) == 1.
        double lo = 0d, hi = 0.999d;
        for (int i = 0; i < MaxIterations; i++)
        {
            var mid = 0.5 * (lo + hi);
            var diff = SumShin(mid, rawImpliedProbabilities, sum) - 1d;
            if (System.Math.Abs(diff) < Tolerance || (hi - lo) < Tolerance)
                return ApplyShin(mid, rawImpliedProbabilities, sum);
            if (diff > 0d) lo = mid; else hi = mid;
        }
        return ApplyShin(0.5 * (lo + hi), rawImpliedProbabilities, sum);
    }

    private static double SumShin(double z, IReadOnlyList<double> p, double sum)
    {
        double s = 0d;
        for (int i = 0; i < p.Count; i++) s += ShinP(z, p[i], sum);
        return s;
    }

    private static double ShinP(double z, double pi, double sum)
    {
        // Guard against z==1 division by zero.
        if (z >= 1d - 1e-12) return pi / sum;
        var inner = z * z + 4d * (1d - z) * pi * pi / sum;
        var sqrt = System.Math.Sqrt(inner);
        return (sqrt - z) / (2d * (1d - z));
    }

    private static double[] ApplyShin(double z, IReadOnlyList<double> p, double sum)
    {
        var result = new double[p.Count];
        double s = 0d;
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = ShinP(z, p[i], sum);
            s += result[i];
        }
        for (int i = 0; i < result.Length; i++) result[i] /= s;
        return result;
    }
}
