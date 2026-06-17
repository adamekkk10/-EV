using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using PlusEV.Core.Domain;

namespace PlusEV.Infrastructure.Odds;

/// <summary>
/// Imports historical odds from a CSV / JSON dataset for backtesting.
/// Expected CSV columns (flexible column order, header-driven):
/// <c>fetched_at, event_id, sport, commence, home, away, book, market, selection, point, decimal_odds, book_updated_at</c>.
/// </summary>
public sealed class CsvHistoricalOddsImporter
{
    private readonly ILogger<CsvHistoricalOddsImporter> _logger;
    public CsvHistoricalOddsImporter(ILogger<CsvHistoricalOddsImporter> logger) => _logger = logger;

    public IReadOnlyList<OddsSnapshot> Import(string path)
    {
        using var reader = new StreamReader(path);
        return ImportFromReader(reader);
    }

    public IReadOnlyList<OddsSnapshot> ImportFromReader(TextReader reader)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
        };
        using var csv = new CsvReader(reader, config);
        var rows = csv.GetRecords<HistoricalRow>().ToList();

        // Group by (event_id, fetched_at) to reconstruct snapshots.
        return rows
            .GroupBy(r => (r.EventId, r.FetchedAt))
            .Select(g => BuildSnapshot(g.ToList()))
            .Where(s => s is not null)
            .Cast<OddsSnapshot>()
            .ToList();
    }

    private OddsSnapshot? BuildSnapshot(IReadOnlyList<HistoricalRow> group)
    {
        var first = group[0];
        if (string.IsNullOrWhiteSpace(first.EventId))
        {
            _logger.LogWarning("Skipping row with missing event id.");
            return null;
        }
        var ev = new SportingEvent
        {
            Id = first.EventId,
            Sport = new Sport(first.Sport, first.Sport),
            HomeTeam = first.Home,
            AwayTeam = first.Away,
            Commence = first.Commence,
        };
        var byBookMarket = group.GroupBy(r => (r.Book, r.Market));
        var quotes = new List<MarketQuote>();
        foreach (var bm in byBookMarket)
        {
            if (!TryParseMarket(bm.Key.Market, out var market)) continue;
            var outcomes = bm.Select(r => new Outcome
            {
                Name = r.Selection,
                DecimalOdds = r.DecimalOdds,
                Point = r.Point,
            }).ToList();
            quotes.Add(new MarketQuote
            {
                Book = Bookmaker.Resolve(bm.Key.Book),
                Market = market,
                Outcomes = outcomes,
                BookUpdatedAt = bm.First().BookUpdatedAt ?? first.FetchedAt,
                FetchedAt = first.FetchedAt,
            });
        }
        return new OddsSnapshot { Event = ev, Quotes = quotes, FetchedAt = first.FetchedAt };
    }

    private static bool TryParseMarket(string key, out MarketType market)
    {
        switch (key.Trim().ToLowerInvariant())
        {
            case "h2h": case "moneyline": case "ml": market = MarketType.H2H; return true;
            case "spread": case "spreads": case "handicap": market = MarketType.Spread; return true;
            case "total": case "totals": case "over_under": market = MarketType.Total; return true;
            default: market = MarketType.Prop; return false;
        }
    }

    private sealed class HistoricalRow
    {
        public DateTimeOffset FetchedAt { get; set; }
        public string EventId { get; set; } = "";
        public string Sport { get; set; } = "";
        public DateTimeOffset Commence { get; set; }
        public string Home { get; set; } = "";
        public string Away { get; set; } = "";
        public string Book { get; set; } = "";
        public string Market { get; set; } = "";
        public string Selection { get; set; } = "";
        public double? Point { get; set; }
        public decimal DecimalOdds { get; set; }
        public DateTimeOffset? BookUpdatedAt { get; set; }
    }
}
