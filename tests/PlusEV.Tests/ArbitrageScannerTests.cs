using FluentAssertions;
using PlusEV.Core.Domain;
using PlusEV.Core.Engine;
using Xunit;

namespace PlusEV.Tests;

public class ArbitrageScannerTests
{
    [Fact]
    public void Detects_two_way_arbitrage()
    {
        // 2.05 at one book + 2.05 at another => Σ(1/dec) = 0.976 < 1 (~2.4% arb).
        var ev = new SportingEvent
        {
            Id = "e1", Sport = Sport.Nba, HomeTeam = "Home", AwayTeam = "Away",
            Commence = DateTimeOffset.UtcNow.AddHours(6),
        };
        var a = new MarketQuote
        {
            Book = Bookmaker.DraftKings, Market = MarketType.H2H,
            Outcomes = new[]
            {
                new Outcome { Name = "Home", DecimalOdds = 2.05m },
                new Outcome { Name = "Away", DecimalOdds = 1.80m },
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
        var b = new MarketQuote
        {
            Book = Bookmaker.FanDuel, Market = MarketType.H2H,
            Outcomes = new[]
            {
                new Outcome { Name = "Home", DecimalOdds = 1.80m },
                new Outcome { Name = "Away", DecimalOdds = 2.05m },
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
        var snap = new OddsSnapshot { Event = ev, Quotes = new[] { a, b }, FetchedAt = DateTimeOffset.UtcNow };

        var scanner = new ArbitrageScanner();
        var arbs = scanner.Scan(snap, DateTimeOffset.UtcNow);
        arbs.Should().ContainSingle();
        arbs[0].GuaranteedReturnPercent.Should().BeGreaterThan(0m);
        arbs[0].Legs.Sum(l => l.StakeFraction).Should().BeApproximately(1m, 0.001m);
    }

    [Fact]
    public void No_arbitrage_when_overround_exceeds_one()
    {
        var ev = new SportingEvent
        {
            Id = "e1", Sport = Sport.Nba, HomeTeam = "Home", AwayTeam = "Away",
            Commence = DateTimeOffset.UtcNow.AddHours(6),
        };
        var a = new MarketQuote
        {
            Book = Bookmaker.DraftKings, Market = MarketType.H2H,
            Outcomes = new[]
            {
                new Outcome { Name = "Home", DecimalOdds = 1.91m },
                new Outcome { Name = "Away", DecimalOdds = 1.91m },
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
        var snap = new OddsSnapshot { Event = ev, Quotes = new[] { a }, FetchedAt = DateTimeOffset.UtcNow };
        new ArbitrageScanner().Scan(snap, DateTimeOffset.UtcNow).Should().BeEmpty();
    }
}
