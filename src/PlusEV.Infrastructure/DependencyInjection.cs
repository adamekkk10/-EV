using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Engine;
using PlusEV.Core.Options;
using PlusEV.Infrastructure.Alerts;
using PlusEV.Infrastructure.Odds;
using PlusEV.Infrastructure.Persistence;
using Polly;
using Polly.Extensions.Http;
using Refit;

namespace PlusEV.Infrastructure;

/// <summary>Wires up infrastructure services. Called from the UI composition root.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPlusEvInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options binding.
        services.Configure<OddsApiOptions>(configuration.GetSection(OddsApiOptions.SectionName));
        services.Configure<EvEngineOptions>(configuration.GetSection(EvEngineOptions.SectionName));
        services.Configure<BankrollOptions>(configuration.GetSection(BankrollOptions.SectionName));
        services.Configure<DemoBotOptions>(configuration.GetSection(DemoBotOptions.SectionName));
        services.Configure<LineHistoryOptions>(configuration.GetSection(LineHistoryOptions.SectionName));
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<DiscordOptions>(configuration.GetSection(DiscordOptions.SectionName));
        services.Configure<DesktopAlertOptions>(configuration.GetSection(DesktopAlertOptions.SectionName));

        // Database. The pooled factory is injectable into singletons / UI view-models that
        // need to open a short-lived context. Scoped PlusEvDbContext instances are still
        // available for scoped services (BankrollService, BetTracker, etc.) via the shim below.
        var connectionString = configuration.GetConnectionString("PlusEvDb")
            ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "plusev.db")}";
        services.AddDbContextFactory<PlusEvDbContext>(opts => opts.UseSqlite(connectionString));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<PlusEvDbContext>>().CreateDbContext());

        // Odds API client with Polly (retry + circuit breaker).
        var oddsBase = configuration.GetSection(OddsApiOptions.SectionName).GetValue<string>("BaseUrl")
            ?? "https://api.the-odds-api.com/v4";
        services.AddRefitClient<ITheOddsApiClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(oddsBase);
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Discord uses a named HttpClient.
        services.AddHttpClient(nameof(DiscordAlertChannel))
            .AddPolicyHandler(GetRetryPolicy());

        services.AddSingleton<IOddsProvider, TheOddsApiProvider>();
        services.AddSingleton<IEventResultProvider, TheOddsApiResultProvider>();
        services.AddSingleton<CsvHistoricalOddsImporter>();

        services.AddSingleton<IDesktopNotifier, NullDesktopNotifier>();
        services.AddSingleton<IAlertChannel, TelegramAlertChannel>();
        services.AddSingleton<IAlertChannel, DiscordAlertChannel>();
        services.AddSingleton<IAlertChannel, DesktopAlertChannel>();
        services.AddSingleton<AlertDispatcher>();
        services.AddSingleton<IAlertDispatcher>(sp => sp.GetRequiredService<AlertDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<AlertDispatcher>());

        services.AddSingleton<EvEngine>();
        services.AddSingleton<ArbitrageScanner>();
        services.AddSingleton<MiddleScanner>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * System.Math.Pow(2, attempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));
}
