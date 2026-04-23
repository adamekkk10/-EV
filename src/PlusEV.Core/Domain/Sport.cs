namespace PlusEV.Core.Domain;

/// <summary>
/// A sport identifier. Uses the string keys from The Odds API
/// (see <c>https://the-odds-api.com/sports-odds-data/sports-apis.html</c>)
/// so ingestion is a straight passthrough.
/// </summary>
public readonly record struct Sport(string Key, string Title)
{
    public static readonly Sport Nfl = new("americanfootball_nfl", "NFL");
    public static readonly Sport Ncaaf = new("americanfootball_ncaaf", "NCAAF");
    public static readonly Sport Nba = new("basketball_nba", "NBA");
    public static readonly Sport Ncaab = new("basketball_ncaab", "NCAAB");
    public static readonly Sport Mlb = new("baseball_mlb", "MLB");
    public static readonly Sport Nhl = new("icehockey_nhl", "NHL");
    public static readonly Sport SoccerEplEng = new("soccer_epl", "EPL");
    public static readonly Sport TennisAtp = new("tennis_atp", "ATP");
    public static readonly Sport Mma = new("mma_mixed_martial_arts", "MMA");

    public override string ToString() => Title;
}
