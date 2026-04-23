using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Domain;
using PlusEV.Core.Options;
using Refit;

namespace PlusEV.Infrastructure.Odds;

/// <summary>
/// <see cref="IOddsProvider"/> backed by The Odds API v4.
/// Honours rate limit headers so the UI status bar can show remaining quota.
/// </summary>
public sealed class TheOddsApiProvider : IOddsProvider
{
    private readonly ITheOddsApiClient _client;
    private readonly OddsApiOptions _options;
    private readonly ILogger<TheOddsApiProvider> _logger;
    private readonly object _lock = new();
    private RateLimitInfo? _lastRateLimit;

    public TheOddsApiProvider(
        ITheOddsApiClient client,
        IOptions<OddsApiOptions> options,
        ILogger<TheOddsApiProvider> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public string Key => "the-odds-api";
    public string DisplayName => "The Odds API";

    public RateLimitInfo? LastKnownRateLimit
    {
        get { lock (_lock) return _lastRateLimit; }
    }

    public async Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken ct = default)
    {
        var response = await _client.GetSportsAsync(_options.ApiKey, ct).ConfigureAwait(false);
        CaptureRateLimit(response);
        response.EnsureSuccessStatusCode();
        return response.Content!
            .Where(s => s.Active)
            .Select(s => new Sport(s.Key, s.Title))
            .ToList();
    }

    public async Task<IReadOnlyList<OddsSnapshot>> GetOddsAsync(
        Sport sport,
        IReadOnlyCollection<MarketType> markets,
        IReadOnlyCollection<string>? books = null,
        CancellationToken ct = default)
    {
        var marketParam = ToMarketParam(markets);
        var bookmakerParam = books is { Count: > 0 } ? string.Join(",", books) : null;

        var response = await _client.GetOddsAsync(
            sport.Key,
            _options.ApiKey,
            _options.Region,
            marketParam,
            _options.OddsFormat,
            bookmakerParam,
            ct).ConfigureAwait(false);
        CaptureRateLimit(response);
        response.EnsureSuccessStatusCode();

        var fetched = DateTimeOffset.UtcNow;
        var list = new List<OddsSnapshot>(response.Content?.Count ?? 0);
        foreach (var e in response.Content ?? new())
        {
            list.Add(ToSnapshot(e, fetched));
        }
        _logger.LogDebug("Fetched {Count} events for sport {Sport}", list.Count, sport.Key);
        return list;
    }

    public async Task<IReadOnlyList<OddsSnapshot>> GetHistoricalOddsAsync(
        Sport sport, DateTimeOffset at, IReadOnlyCollection<MarketType> markets, CancellationToken ct = default)
    {
        var marketParam = ToMarketParam(markets);
        var response = await _client.GetHistoricalOddsAsync(
            sport.Key,
            _options.ApiKey,
            _options.Region,
            marketParam,
            _options.OddsFormat,
            at.ToString("o"),
            ct).ConfigureAwait(false);
        CaptureRateLimit(response);
        response.EnsureSuccessStatusCode();

        var fetched = response.Content?.Timestamp ?? at;
        var list = new List<OddsSnapshot>();
        foreach (var e in response.Content?.Data ?? new())
            list.Add(ToSnapshot(e, fetched));
        return list;
    }

    private static OddsSnapshot ToSnapshot(TheOddsApiEventDto e, DateTimeOffset fetched)
    {
        var ev = new SportingEvent
        {
            Id = e.Id,
            Sport = new Sport(e.SportKey, e.SportTitle),
            HomeTeam = e.HomeTeam,
            AwayTeam = e.AwayTeam,
            Commence = e.CommenceTime,
        };

        var quotes = new List<MarketQuote>();
        foreach (var bm in e.Bookmakers)
        {
            foreach (var m in bm.Markets)
            {
                if (!TryParseMarket(m.Key, out var marketType)) continue;
                var outcomes = m.Outcomes.Select(o => new Outcome
                {
                    Name = o.Name,
                    DecimalOdds = o.Price,
                    Point = o.Point,
                }).ToList();
                if (outcomes.Count == 0) continue;
                quotes.Add(new MarketQuote
                {
                    Book = Bookmaker.Resolve(bm.Key),
                    Market = marketType,
                    Outcomes = outcomes,
                    BookUpdatedAt = m.LastUpdate,
                    FetchedAt = fetched,
                });
            }
        }
        return new OddsSnapshot { Event = ev, Quotes = quotes, FetchedAt = fetched };
    }

    private static bool TryParseMarket(string key, out MarketType market)
    {
        switch (key.ToLowerInvariant())
        {
            case "h2h": market = MarketType.H2H; return true;
            case "spreads": market = MarketType.Spread; return true;
            case "totals": market = MarketType.Total; return true;
            default: market = MarketType.Prop; return false;
        }
    }

    private static string ToMarketParam(IReadOnlyCollection<MarketType> markets)
    {
        var parts = new List<string>(markets.Count);
        foreach (var m in markets)
        {
            parts.Add(m switch
            {
                MarketType.H2H => "h2h",
                MarketType.Spread => "spreads",
                MarketType.Total => "totals",
                _ => "h2h",
            });
        }
        return parts.Count == 0 ? "h2h" : string.Join(",", parts.Distinct());
    }

    private void CaptureRateLimit<T>(IApiResponse<T> response)
    {
        if (response.Headers.TryGetValues("x-requests-remaining", out var remainingValues) &&
            int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
        {
            var used = 0;
            if (response.Headers.TryGetValues("x-requests-used", out var usedValues))
                int.TryParse(usedValues.FirstOrDefault(), out used);
            lock (_lock) _lastRateLimit = new RateLimitInfo(remaining, used, DateTimeOffset.UtcNow);
        }
    }
}
