using Microsoft.Extensions.DependencyInjection;
using PlusEV.Application.Backtesting;
using PlusEV.Application.Demo;
using PlusEV.Application.Services;

namespace PlusEV.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPlusEvApplication(this IServiceCollection services)
    {
        services.AddSingleton<OpportunityFeed>();
        services.AddSingleton<LatencyTracker>();
        services.AddScoped<BankrollService>();
        services.AddScoped<BetTracker>();
        services.AddScoped<RealityCheckService>();
        services.AddSingleton<DemoBot>();
        services.AddSingleton<Backtester>();
        services.AddHostedService<OddsIngestionService>();
        return services;
    }
}
