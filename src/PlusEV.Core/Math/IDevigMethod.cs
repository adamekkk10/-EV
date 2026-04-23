namespace PlusEV.Core.Math;

/// <summary>
/// Strategy for removing the vig (bookmaker's margin) from a set of raw implied
/// probabilities to estimate the underlying "true" probabilities.
/// Implementations must be deterministic and pure.
/// </summary>
public interface IDevigMethod
{
    /// <summary>A stable key for logs / UI radio buttons.</summary>
    string Key { get; }

    /// <summary>Human label.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Remove the vig from raw implied probabilities.
    /// The returned array has the same length as the input and sums to 1
    /// (within 1e-9 tolerance).
    /// </summary>
    /// <param name="rawImpliedProbabilities">Implied probabilities as 1/decimal_odds for each outcome.</param>
    double[] Devig(IReadOnlyList<double> rawImpliedProbabilities);
}
