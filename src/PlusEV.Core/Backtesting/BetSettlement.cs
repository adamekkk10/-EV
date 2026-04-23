using PlusEV.Core.Abstractions;
using PlusEV.Core.Domain;

namespace PlusEV.Core.Backtesting;

/// <summary>
/// Settles a pending bet against a concrete <see cref="EventResult"/>.
/// Pure function so it can be shared by the demo bot and the backtester.
/// </summary>
public static class BetSettlement
{
    public static BetResult Settle(Bet bet, EventResult result, string? homeTeam = null, string? awayTeam = null)
    {
        ArgumentNullException.ThrowIfNull(bet);
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Completed) return BetResult.Pending;
        if (result.HomeScore is null || result.AwayScore is null) return BetResult.Void;

        var home = result.HomeScore.Value;
        var away = result.AwayScore.Value;

        return bet.Market switch
        {
            MarketType.H2H => SettleH2H(bet, home, away, homeTeam, awayTeam),
            MarketType.Spread => SettleSpread(bet, home, away, homeTeam, awayTeam),
            MarketType.Total => SettleTotal(bet, home, away),
            _ => BetResult.Void,
        };
    }

    private static BetResult SettleH2H(Bet bet, int home, int away, string? homeTeam, string? awayTeam)
    {
        if (home == away) return BetResult.Push;  // draw (rare in H2H-without-draw markets)
        var homeWon = home > away;

        if (IsHomeSelection(bet.Selection, homeTeam, awayTeam)) return homeWon ? BetResult.Won : BetResult.Lost;
        if (IsAwaySelection(bet.Selection, homeTeam, awayTeam)) return homeWon ? BetResult.Lost : BetResult.Won;
        return BetResult.Void;
    }

    private static BetResult SettleSpread(Bet bet, int home, int away, string? homeTeam, string? awayTeam)
    {
        if (bet.Point is null) return BetResult.Void;
        var homeMargin = home - away;  // positive = home won by that much
        var isHome = IsHomeSelection(bet.Selection, homeTeam, awayTeam);
        var isAway = IsAwaySelection(bet.Selection, homeTeam, awayTeam);
        if (!isHome && !isAway) return BetResult.Void;

        var adjusted = isHome ? homeMargin + bet.Point.Value : -homeMargin + bet.Point.Value;
        if (System.Math.Abs(adjusted) < 1e-9) return BetResult.Push;
        return adjusted > 0 ? BetResult.Won : BetResult.Lost;
    }

    private static BetResult SettleTotal(Bet bet, int home, int away)
    {
        if (bet.Point is null) return BetResult.Void;
        var total = home + away;
        var isOver = bet.Selection.Contains("Over", StringComparison.OrdinalIgnoreCase);
        var isUnder = bet.Selection.Contains("Under", StringComparison.OrdinalIgnoreCase);
        if (!isOver && !isUnder) return BetResult.Void;
        var diff = total - bet.Point.Value;
        if (System.Math.Abs(diff) < 1e-9) return BetResult.Push;
        if (isOver) return diff > 0 ? BetResult.Won : BetResult.Lost;
        return diff < 0 ? BetResult.Won : BetResult.Lost;
    }

    private static bool IsHomeSelection(string selection, string? home, string? away)
    {
        if (home is null) return false;
        return selection.Contains(home, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAwaySelection(string selection, string? home, string? away)
    {
        if (away is null) return false;
        return selection.Contains(away, StringComparison.OrdinalIgnoreCase);
    }
}
