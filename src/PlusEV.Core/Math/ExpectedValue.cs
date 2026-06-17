namespace PlusEV.Core.Math;

/// <summary>
/// Expected-value computation: the headline number in the whole app.
/// </summary>
public static class ExpectedValue
{
    /// <summary>
    /// EV% = true_probability * decimal_odds − 1.  Positive means profitable in expectation.
    /// e.g. trueProb 0.55 at 2.00 =&gt; 0.10 (10% EV).
    /// </summary>
    public static double Compute(double trueProbability, double decimalOdds)
    {
        if (trueProbability <= 0d || trueProbability >= 1d)
            throw new ArgumentOutOfRangeException(nameof(trueProbability));
        if (decimalOdds <= 1d)
            throw new ArgumentOutOfRangeException(nameof(decimalOdds));
        return trueProbability * decimalOdds - 1d;
    }

    /// <summary>
    /// Closing Line Value. Positive CLV is the strongest long-run indicator that a
    /// bettor is beating the market. Computed as <c>decimal_odds_taken / closing_decimal − 1</c>
    /// (price CLV) which for the conventional interpretation on a binary is equivalent to
    /// the reciprocal of closing implied probability relative to entry.
    /// </summary>
    public static double Clv(double decimalOddsTaken, double closingDecimalOdds)
    {
        if (decimalOddsTaken <= 1d) throw new ArgumentOutOfRangeException(nameof(decimalOddsTaken));
        if (closingDecimalOdds <= 1d) throw new ArgumentOutOfRangeException(nameof(closingDecimalOdds));
        // Probability-space CLV: closing implied prob / taken implied prob − 1
        //   = (1/closing) / (1/taken) − 1 = taken/closing − 1.
        return decimalOddsTaken / closingDecimalOdds - 1d;
    }
}
