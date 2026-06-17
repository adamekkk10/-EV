using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PlusEV.Application.Services;
using PlusEV.Core.Abstractions;

namespace PlusEV.UI.ViewModels;

/// <summary>
/// Always-visible footer. Driven by a 1-Hz timer that reads from the infrastructure services.
/// </summary>
public sealed partial class StatusBarViewModel : ObservableObject, IDisposable
{
    private readonly LatencyTracker _latency;
    private readonly IOddsProvider _provider;
    private readonly DispatcherTimer _timer;

    public StatusBarViewModel(LatencyTracker latency, IOddsProvider provider)
    {
        _latency = latency;
        _provider = provider;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick());
        _timer.Start();
    }

    [ObservableProperty] private string _currentModeLabel = "LIVE";
    [ObservableProperty] private int _apiRemaining;
    [ObservableProperty] private int _apiUsed;
    [ObservableProperty] private string _lastRefresh = "--";
    [ObservableProperty] private double _averageFetchLatencyMs;
    [ObservableProperty] private double _averageIdentifyLatencyMs;
    [ObservableProperty] private string _connection = "Ready";
    [ObservableProperty] private string _demoBotStatus = "Stopped";

    private void Tick()
    {
        var rl = _provider.LastKnownRateLimit;
        if (rl is not null)
        {
            ApiRemaining = rl.Remaining;
            ApiUsed = rl.Used;
            LastRefresh = rl.ObservedAt.ToLocalTime().ToString("HH:mm:ss");
        }
        AverageFetchLatencyMs = _latency.AverageFetchLatencyMs;
        AverageIdentifyLatencyMs = _latency.AverageIdentifyLatencyMs;
    }

    public void Dispose() => _timer.Stop();
}
