namespace PlusEV.Core.Math;

/// <summary>
/// Kelly staking utilities.  Full Kelly maximises expected log bankroll but is
/// practically too aggressive for sports betting because (a) true edge is estimated
/// with noise and (b) book limits punish streaky sizing. Default is 0.25 (quarter Kelly).
/// </summary>
public static class Kelly
{
    /// <summary>
    /// Full Kelly fraction for a single binary bet at decimal odds.
    /// Formula: <c>f = (b·p − q) / b</c> where <c>b = decimalOdds − 1</c>, <c>q = 1 − p</c>.
    /// Returns 0 for negative-EV bets (never bet with negative edge).
    /// </summary>
    public static double Fraction(double trueProbability, double decimalOdds)
    {
        if (trueProbability <= 0d || trueProbability >= 1d)
            throw new ArgumentOutOfRangeException(nameof(trueProbability), "Probability must be in (0,1).");
        if (decimalOdds <= 1d)
            throw new ArgumentOutOfRangeException(nameof(decimalOdds), "Decimal odds must be > 1.");

        var b = decimalOdds - 1d;
        var q = 1d - trueProbability;
        var f = (b * trueProbability - q) / b;
        return f <= 0d ? 0d : f;
    }

    /// <summary>
    /// Stake recommendation combining fractional Kelly, a hard percentage cap of the
    /// bankroll, and a soft cap at a multiple of the user's rolling average stake
    /// (to avoid tripping book limits on high-confidence bets). Any of these caps
    /// may be disabled by passing non-positive / null.
    /// </summary>
    /// <param name="bankroll">Current bankroll.</param>
    /// <param name="trueProbability">Estimated true probability of the bet winning.</param>
    /// <param name="decimalOdds">Offered decimal odds.</param>
    /// <param name="kellyFraction">Fraction of full Kelly to stake (e.g. 0.25 = quarter Kelly).</param>
    /// <param name="hardCapFractionOfBankroll">Absolute max stake as fraction of bankroll (e.g. 0.02 = 2%).</param>
    /// <param name="rollingAverageStake">Rolling average of the user's recent stakes.
    /// Null / zero disables the soft cap.</param>
    /// <param name="softCapMultiple">Multiple of the rolling average that forms the soft ceiling.
    /// Default 5x per the spec.</param>
    public static decimal RecommendedStake(
        decimal bankroll,
        double trueProbability,
        double decimalOdds,
        double kellyFraction = 0.25d,
        double hardCapFractionOfBankroll = 0.02d,
        decimal? rollingAverageStake = null,
        double softCapMultiple = 5d)
    {
        if (bankroll <= 0m) return 0m;
        if (kellyFraction <= 0d) throw new ArgumentOutOfRangeException(nameof(kellyFraction));

        var full = Fraction(trueProbability, decimalOdds);
        if (full <= 0d) return 0m;

        var fractional = full * kellyFraction;
        var stake = (decimal)fractional * bankroll;

        if (hardCapFractionOfBankroll > 0d)
        {
            var hardCap = bankroll * (decimal)hardCapFractionOfBankroll;
            if (stake > hardCap) stake = hardCap;
        }

        if (rollingAverageStake is { } avg && avg > 0m && softCapMultiple > 0d)
        {
            var softCap = avg * (decimal)softCapMultiple;
            if (stake > softCap) stake = softCap;
        }

        return System.Math.Round(stake, 2, MidpointRounding.ToZero);
    }

    /// <summary>
    /// Flat stake: a fixed fraction of bankroll regardless of edge. Useful as an
    /// alternative mode when the user wants to test strategy separately from sizing.
    /// </summary>
    public static decimal FlatStake(decimal bankroll, double fractionOfBankroll)
    {
        if (bankroll <= 0m) return 0m;
        if (fractionOfBankroll <= 0d) return 0m;
        return System.Math.Round(bankroll * (decimal)fractionOfBankroll, 2, MidpointRounding.ToZero);
    }
}
