namespace PlusEV.Core.Math;

/// <summary>
/// Proportional / multiplicative devig. Each outcome's fair probability is
/// <c>p_i / sum(p_j)</c>. Fast, simple, and the standard baseline. Tends to
/// underprice longshots relative to Shin's method because it removes vig
/// uniformly across all outcomes.
/// </summary>
public sealed class MultiplicativeDevig : IDevigMethod
{
    public string Key => "multiplicative";
    public string DisplayName => "Multiplicative (proportional)";

    public double[] Devig(IReadOnlyList<double> rawImpliedProbabilities)
    {
        ArgumentNullException.ThrowIfNull(rawImpliedProbabilities);
        if (rawImpliedProbabilities.Count == 0)
            throw new ArgumentException("At least one probability required.", nameof(rawImpliedProbabilities));

        double sum = 0d;
        for (int i = 0; i < rawImpliedProbabilities.Count; i++)
        {
            var p = rawImpliedProbabilities[i];
            if (p <= 0d) throw new ArgumentException("Probabilities must be > 0.", nameof(rawImpliedProbabilities));
            sum += p;
        }

        if (sum <= 0d) throw new InvalidOperationException("Sum of implied probabilities must be positive.");

        var result = new double[rawImpliedProbabilities.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = rawImpliedProbabilities[i] / sum;
        return result;
    }
}
