namespace PlusEV.Core.Domain;

/// <summary>
/// A sportsbook. <see cref="IsSharp"/> marks books we trust to produce efficient lines
/// (Pinnacle, Circa, and to a lesser extent Bookmaker). These are used for devigging
/// and the market-consensus sanity check.
/// </summary>
public sealed record Bookmaker(string Key, string Title, bool IsSharp)
{
    public static readonly Bookmaker Pinnacle = new("pinnacle", "Pinnacle", IsSharp: true);
    public static readonly Bookmaker Circa = new("circasports", "Circa Sports", IsSharp: true);
    public static readonly Bookmaker Bookmaker = new("bookmaker", "Bookmaker.eu", IsSharp: true);
    public static readonly Bookmaker DraftKings = new("draftkings", "DraftKings", IsSharp: false);
    public static readonly Bookmaker FanDuel = new("fanduel", "FanDuel", IsSharp: false);
    public static readonly Bookmaker Bet365 = new("bet365", "bet365", IsSharp: false);
    public static readonly Bookmaker BetMGM = new("betmgm", "BetMGM", IsSharp: false);
    public static readonly Bookmaker Caesars = new("williamhill_us", "Caesars", IsSharp: false);
    public static readonly Bookmaker PointsBet = new("pointsbetus", "PointsBet", IsSharp: false);

    public static IReadOnlyList<Bookmaker> KnownBooks { get; } = new[]
    {
        Pinnacle, Circa, Bookmaker, DraftKings, FanDuel, Bet365, BetMGM, Caesars, PointsBet,
    };

    public static Bookmaker Resolve(string key) =>
        KnownBooks.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? new Bookmaker(key, key, IsSharp: false);
}
