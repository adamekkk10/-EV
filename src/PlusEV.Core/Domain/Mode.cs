namespace PlusEV.Core.Domain;

/// <summary>
/// Identifies the operating mode for bets, bankroll entries, and settings.
/// LIVE, DEMO and BACKTEST records are partitioned by this enum at the database
/// layer so the three worlds can never be confused.
/// </summary>
public enum Mode
{
    /// <summary>Real money tracking. Opportunities are read-only; users log bets manually.</summary>
    Live = 0,

    /// <summary>Paper-trading. The demo bot "places" bets against a virtual bankroll.</summary>
    Demo = 1,

    /// <summary>Historical simulation driven by the backtester.</summary>
    Backtest = 2,
}
