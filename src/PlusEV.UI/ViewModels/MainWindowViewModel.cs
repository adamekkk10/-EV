using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace PlusEV.UI.ViewModels;

/// <summary>
/// The shell. Owns navigation state, the LIVE/DEMO/BACKTEST mode toggle, and the status bar.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        Navigate("live");
        StatusBar = _services.GetRequiredService<StatusBarViewModel>();
    }

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _currentRoute = "live";

    [ObservableProperty]
    private AppMode _mode = AppMode.Live;

    public StatusBarViewModel StatusBar { get; }

    [RelayCommand]
    public void Navigate(string route)
    {
        CurrentRoute = route;
        CurrentView = route switch
        {
            "live"       => _services.GetRequiredService<LiveOpportunitiesViewModel>(),
            "arb"        => _services.GetRequiredService<ArbitrageMiddleViewModel>(),
            "demo"       => _services.GetRequiredService<DemoBotViewModel>(),
            "backtest"   => _services.GetRequiredService<BacktestViewModel>(),
            "bets"       => _services.GetRequiredService<BetTrackerViewModel>(),
            "bankroll"   => _services.GetRequiredService<BankrollViewModel>(),
            "reality"    => _services.GetRequiredService<RealityCheckViewModel>(),
            "lines"      => _services.GetRequiredService<LineHistoryViewModel>(),
            "alerts"     => _services.GetRequiredService<AlertsViewModel>(),
            "settings"   => _services.GetRequiredService<SettingsViewModel>(),
            _            => _services.GetRequiredService<LiveOpportunitiesViewModel>(),
        };
    }

    [RelayCommand]
    public void SetMode(AppMode mode) => Mode = mode;
}

public abstract partial class ViewModelBase : ObservableObject { }
