using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlusEV.Application;
using PlusEV.Infrastructure;
using PlusEV.Infrastructure.Persistence;
using PlusEV.UI.Services;
using PlusEV.UI.ViewModels;
using PlusEV.UI.Views;
using Serilog;

namespace PlusEV.UI;

/// <summary>
/// Avalonia application. Composes the generic host (DI, logging, configuration)
/// and exposes the service provider to viewmodels.
/// </summary>
public partial class App : Avalonia.Application
{
    private IHost? _host;

    public static IServiceProvider Services => ((App)Current!)._host!.Services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _host = BuildHost();
        _host.Start();

        _ = DatabaseInitializer.EnsureReadyAsync(_host.Services);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = _host.Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.Exit += (_, _) => _host.StopAsync().GetAwaiter().GetResult();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "PLUSEV_");

        builder.Services.AddPlusEvInfrastructure(builder.Configuration);
        builder.Services.AddPlusEvApplication();

        // Swap the no-op desktop notifier for the platform-specific one.
        builder.Services.AddSingleton<Infrastructure.Alerts.IDesktopNotifier, PlatformDesktopNotifier>();

        // ViewModels.
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<StatusBarViewModel>();
        builder.Services.AddTransient<LiveOpportunitiesViewModel>();
        builder.Services.AddTransient<ArbitrageMiddleViewModel>();
        builder.Services.AddTransient<DemoBotViewModel>();
        builder.Services.AddTransient<BacktestViewModel>();
        builder.Services.AddTransient<BetTrackerViewModel>();
        builder.Services.AddTransient<BankrollViewModel>();
        builder.Services.AddTransient<RealityCheckViewModel>();
        builder.Services.AddTransient<LineHistoryViewModel>();
        builder.Services.AddTransient<AlertsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddSingleton<UiDispatcher>();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(AppContext.BaseDirectory, "logs/plusev-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .WriteTo.Console()
            .CreateLogger();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        return builder.Build();
    }
}
