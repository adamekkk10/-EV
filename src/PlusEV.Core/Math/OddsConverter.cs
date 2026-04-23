namespace PlusEV.Core.Math;

/// <summary>
/// Conversion between American (-110, +150), decimal (1.91, 2.50), and
/// probabilistic (0.524) representations of sportsbook prices.
/// All methods are pure.
/// </summary>
public static class OddsConverter
{
    /// <summary>Convert American odds to decimal. -110 =&gt; 1.909..., +150 =&gt; 2.50.</summary>
    public static decimal AmericanToDecimal(int american)
    {
        if (american == 0) throw new ArgumentOutOfRangeException(nameof(american), "American odds cannot be 0.");
        return american > 0
            ? 1m + (american / 100m)
            : 1m + (100m / -american);
    }

    /// <summary>
    /// Convert decimal odds to American. Rounds half-away-from-zero to match industry display.
    /// 1.91 =&gt; -110, 2.50 =&gt; +150.
    /// </summary>
    public static int DecimalToAmerican(decimal dec)
    {
        if (dec <= 1m) throw new ArgumentOutOfRangeException(nameof(dec), "Decimal odds must be > 1.");
        return dec >= 2m
            ? (int)System.Math.Round((double)((dec - 1m) * 100m), MidpointRounding.AwayFromZero)
            : -(int)System.Math.Round((double)(100m / (dec - 1m)), MidpointRounding.AwayFromZero);
    }

    /// <summary>Raw (with-vig) implied probability of a decimal price. 1.91 =&gt; 0.5236.</summary>
    public static decimal DecimalToImpliedProbability(decimal dec)
    {
        if (dec <= 0m) throw new ArgumentOutOfRangeException(nameof(dec));
        return 1m / dec;
    }

    /// <summary>Fair decimal odds for a given probability. 0.5 =&gt; 2.0.</summary>
    public static decimal ProbabilityToDecimal(decimal probability)
    {
        if (probability <= 0m || probability >= 1m)
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be in (0,1).");
        return 1m / probability;
    }

    /// <summary>Fair American odds for a given probability.</summary>
    public static int ProbabilityToAmerican(decimal probability) =>
        DecimalToAmerican(ProbabilityToDecimal(probability));

    /// <summary>American odds parsed from common display strings like "-110", "+150", "EVEN".</summary>
    public static int ParseAmerican(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var t = text.Trim();
        if (string.Equals(t, "EVEN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "EV", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "PK", StringComparison.OrdinalIgnoreCase)) return 100;
        if (t.StartsWith('+')) t = t[1..];
        return int.Parse(t, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Display string for American odds with a leading + on positives.</summary>
    public static string FormatAmerican(int american) =>
        american > 0 ? $"+{american}" : american.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
