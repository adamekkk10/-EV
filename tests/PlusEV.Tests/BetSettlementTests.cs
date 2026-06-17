using FluentAssertions;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Backtesting;
using PlusEV.Core.Domain;
using Xunit;

namespace PlusEV.Tests;

public class BetSettlementTests
{
    private static Bet MakeBet(MarketType market, string selection, double? point) => new()
    {
        Mode = Mode.Backtest,
        EventId = "e1",
        EventLabel = "Home @ Away",
        Sport = "nba",
        Market = market,
        Selection = selection,
        Point = point,
        BookKey = "pinnacle",
        BookTitle = "Pinnacle",
        DecimalOdds = 2.0m,
        AmericanOdds = 100,
        Stake = 10m,
        PlacedAt = DateTimeOffset.UtcNow,
        EventStart = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void H2H_home_win_settles_winning()
    {
        var bet = MakeBet(MarketType.H2H, "Lakers", null);
        var result = new EventResult("e1", true, HomeScore: 110, AwayScore: 100, DateTimeOffset.UtcNow);
        BetSettlement.Settle(bet, result, homeTeam: "Lakers", awayTeam: "Celtics").Should().Be(BetResult.Won);
    }

    [Fact]
    public void H2H_away_win_loses_bet_on_home()
    {
        var bet = MakeBet(MarketType.H2H, "Lakers", null);
        var result = new EventResult("e1", true, HomeScore: 98, AwayScore: 100, DateTimeOffset.UtcNow);
        BetSettlement.Settle(bet, result, homeTeam: "Lakers", awayTeam: "Celtics").Should().Be(BetResult.Lost);
    }

    [Fact]
    public void Spread_push_on_exact_margin()
    {
        var bet = MakeBet(MarketType.Spread, "Lakers -5", point: -5d);
        var result = new EventResult("e1", true, 105, 100, DateTimeOffset.UtcNow);
        BetSettlement.Settle(bet, result, homeTeam: "Lakers", awayTeam: "Celtics").Should().Be(BetResult.Push);
    }

    [Fact]
    public void Spread_win_when_margin_exceeds_line()
    {
        var bet = MakeBet(MarketType.Spread, "Lakers -5.5", point: -5.5d);
        var result = new EventResult("e1", true, 110, 100, DateTimeOffset.UtcNow);
        BetSettlement.Settle(bet, result, homeTeam: "Lakers", awayTeam: "Celtics").Should().Be(BetResult.Won);
    }

    [Fact]
    public void Total_over_wins_when_score_exceeds_line()
    {
        var bet = MakeBet(MarketType.Total, "Over 210.5", point: 210.5d);
        var result = new EventResult("e1", true, 110, 105, DateTimeOffset.UtcNow);
        BetSettlement.Settle(bet, result).Should().Be(BetResult.Won);
    }

    [Fact]
    public void Total_under_wins_when_score_below_line()
    {
        var bet = MakeBet(MarketType.Total, "Under 210.5", point: 210.5d);
        var result = new EventResult("e1", true, 100, 100, DateTimeOffset.UtcNow);
        BetSettlement.Settle(bet, result).Should().Be(BetResult.Won);
    }

    [Fact]
    public void Incomplete_event_returns_pending()
    {
        var bet = MakeBet(MarketType.H2H, "Lakers", null);
        var result = new EventResult("e1", Completed: false, null, null, DateTimeOffset.UtcNow);
        BetSettlement.Settle(bet, result, homeTeam: "Lakers", awayTeam: "Celtics").Should().Be(BetResult.Pending);
    }
}
