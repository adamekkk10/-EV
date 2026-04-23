using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PlusEV.Core.Domain;
using PlusEV.Core.Engine;
using Xunit;

namespace PlusEV.Tests;

public class EvEngineTests
{
    private static EvEngine BuildEngine(double minEv = 0.02d)
    {
        var options = Options.Create(new EvEngineOptions
        {
            MinEvPercent = minEv,
            MinConfidence = 0d,   // disable confidence gating for these math tests
            DevigMethodKey = "multiplicative",
            SanityCheckMaxDeltaProbability = 1d,  // disable sanity filter
        });
        return new EvEngine(options, NullLogger<EvEngine>.Instance);
    }

    [Fact]
    public void Finds_positive_ev_when_competitor_is_mispriced_against_pinnacle()
    {
        // Pinnacle 50/50 market at 1.95/1.95 => devigged (multiplicative) to 0.5/0.5.
        // DraftKings offers 2.20 on the same side => EV = 0.5 * 2.20 - 1 = 0.10 (+10%).
        var ev = new SportingEvent
        {
            Id = "e1", Sport = Sport.Nba, HomeTeam = "Home", AwayTeam = "Away",
            Commence = DateTimeOffset.UtcNow.AddHours(6),
        };
        var pinnacle = new MarketQuote
        {
            Book = Bookmaker.Pinnacle, Market = MarketType.H2H,
            Outcomes = new[]
            {
                new Outcome { Name = "Home", DecimalOdds = 1.95m },
                new Outcome { Name = "Away", DecimalOdds = 1.95m },
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
        var dk = new MarketQuote
        {
            Book = Bookmaker.DraftKings, Market = MarketType.H2H,
            Outcomes = new[]
            {
                new Outcome { Name = "Home", DecimalOdds = 2.20m },
                new Outcome { Name = "Away", DecimalOdds = 1.75m },
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
        var snapshot = new OddsSnapshot
        {
            Event = ev,
            Quotes = new[] { pinnacle, dk },
            FetchedAt = DateTimeOffset.UtcNow,
        };

        var engine = BuildEngine();
        var results = engine.Evaluate(snapshot, bankroll: 1000m, rollingAverageStake: null, DateTimeOffset.UtcNow);

        results.Should().NotBeEmpty();
        var home = results.First(r => r.Selection == "Home");
        home.EvPercent.Should().BeApproximately(0.10d, 0.01d);
        home.Book.Key.Should().Be(Bookmaker.DraftKings.Key);
        home.RecommendedStake.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Sanity_check_rejects_obviously_stale_pinnacle_line()
    {
        var options = Options.Create(new EvEngineOptions
        {
            MinEvPercent = 0.02d,
            MinConfidence = 0d,
            DevigMethodKey = "multiplicative",
            SanityCheckMaxDeltaProbability = 0.05d, // strict sanity filter
        });
        var engine = new EvEngine(options, NullLogger<EvEngine>.Instance);

        var ev = new SportingEvent
        {
            Id = "e1", Sport = Sport.Nba, HomeTeam = "Home", AwayTeam = "Away",
            Commence = DateTimeOffset.UtcNow.AddHours(6),
        };
        // Pinnacle is far out of line with everyone else (simulates stale).
        var pinnacle = new MarketQuote
        {
            Book = Bookmaker.Pinnacle, Market = MarketType.H2H,
            Outcomes = new[]
            {
                new Outcome { Name = "Home", DecimalOdds = 3.50m },
                new Outcome { Name = "Away", DecimalOdds = 1.30m },
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
        // Every other book says ~50/50.
        var dk = pinnacle with { };
        var quotes = new List<MarketQuote> { pinnacle };
        foreach (var book in new[] { Bookmaker.DraftKings, Bookmaker.FanDuel, Bookmaker.BetMGM })
        {
            quotes.Add(new MarketQuote
            {
                Book = book, Market = MarketType.H2H,
                Outcomes = new[]
                {
                    new Outcome { Name = "Home", DecimalOdds = 1.95m },
                    new Outcome { Name = "Away", DecimalOdds = 1.95m },
                },
                FetchedAt = DateTimeOffset.UtcNow,
            });
        }
        var snapshot = new OddsSnapshot
        {
            Event = ev, Quotes = quotes, FetchedAt = DateTimeOffset.UtcNow,
        };

        var results = engine.Evaluate(snapshot, 1000m, null, DateTimeOffset.UtcNow);
        results.Should().BeEmpty("sanity filter should reject opportunities when Pinnacle disagrees heavily with consensus");
    }
}
