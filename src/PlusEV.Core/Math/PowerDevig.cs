namespace PlusEV.Core.Math;

/// <summary>
/// Power devig. Finds exponent <c>k</c> such that <c>Σ p_i^k = 1</c> and returns
/// <c>p_i^k</c>. Matches observed favourite-longshot bias better than multiplicative:
/// it removes proportionally more vig from longshots than favourites.
/// </summary>
public sealed class PowerDevig : IDevigMethod
{
    private const int MaxIterations = 80;
    private const double Tolerance = 1e-12;

    public string Key => "power";
    public string DisplayName => "Power (favourite-longshot aware)";

    public double[] Devig(IReadOnlyList<double> rawImpliedProbabilities)
    {
        ArgumentNullException.ThrowIfNull(rawImpliedProbabilities);
        if (rawImpliedProbabilities.Count == 0)
            throw new ArgumentException("At least one probability required.", nameof(rawImpliedProbabilities));

        for (int i = 0; i < rawImpliedProbabilities.Count; i++)
            if (rawImpliedProbabilities[i] <= 0d)
                throw new ArgumentException("Probabilities must be > 0.", nameof(rawImpliedProbabilities));

        // Bracket k.  k=1 gives Σp which is the overround. If Σp > 1 we need k > 1; if < 1, k < 1.
        double lo = 0.0001, hi = 5d;
        // Expand until bracket contains root.
        int guard = 0;
        while (F(hi, rawImpliedProbabilities) > 0d && guard++ < 50) hi *= 2d;
        guard = 0;
        while (F(lo, rawImpliedProbabilities) < 0d && guard++ < 50) lo /= 2d;

        // Bisection. Robust; O(~40) iterations gets us to 1e-12.
        for (int i = 0; i < MaxIterations; i++)
        {
            var mid = 0.5 * (lo + hi);
            var f = F(mid, rawImpliedProbabilities);
            if (System.Math.Abs(f) < Tolerance || (hi - lo) < Tolerance)
            {
                return Apply(mid, rawImpliedProbabilities);
            }
            if (f > 0d) lo = mid; else hi = mid;
        }
        return Apply(0.5 * (lo + hi), rawImpliedProbabilities);
    }

    private static double F(double k, IReadOnlyList<double> p)
    {
        double s = 0d;
        for (int i = 0; i < p.Count; i++) s += System.Math.Pow(p[i], k);
        return s - 1d;
    }

    private static double[] Apply(double k, IReadOnlyList<double> p)
    {
        var result = new double[p.Count];
        double s = 0d;
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = System.Math.Pow(p[i], k);
            s += result[i];
        }
        // Numerically renormalise to exactly 1.
        for (int i = 0; i < result.Length; i++) result[i] /= s;
        return result;
    }
}
