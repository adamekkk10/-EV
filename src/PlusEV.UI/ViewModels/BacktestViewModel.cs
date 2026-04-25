using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PlusEV.Application.Backtesting;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Backtesting;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Odds;
using System.Collections.ObjectModel;

namespace PlusEV.UI.ViewModels;

/// <summary>
/// Backtest form + results. Enforces walk-forward by requiring both in-sample and
/// out-of-sample ranges before <see cref="RunCommand"/> can execute.
/// </summary>
public sealed partial class BacktestViewModel : ViewModelBase
{
    private readonly Backtester _backtester;
    private readonly IServiceProvider _services;

    public BacktestViewModel(Backtester backtester, IServiceProvider services)
    {
        _backtester = backtester;
        _services = services;
    }

    [ObservableProperty] private DateTimeOffset? _inSampleStart;
    [ObservableProperty] private DateTimeOffset? _inSampleEnd;
    [ObservableProperty] private DateTimeOffset? _outOfSampleStart;
    [ObservableProperty] private DateTimeOffset? _outOfSampleEnd;
    [ObservableProperty] private double _minEvPercent = 2d;
    [ObservableProperty] private double _kellyFraction = 0.25d;
    [ObservableProperty] private string _devigMethodKey = "power";
    [ObservableProperty] private decimal _startingBankroll = 1000m;
    [ObservableProperty] private string? _historicalCsvPath;
    [ObservableProperty] private string? _runName;

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _progressText = "Ready";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private BacktestReport? _report;

    public ObservableCollection<string> SelectedBookKeys { get; } = new();
    public ObservableCollection<string> SelectedSportKeys { get; } = new();

    public bool CanRun =>
        InSampleStart.HasValue && InSampleEnd.HasValue &&
        OutOfSampleStart.HasValue && OutOfSampleEnd.HasValue &&
        !IsRunning && !string.IsNullOrWhiteSpace(HistoricalCsvPath);

    partial void OnInSampleStartChanged(DateTimeOffset? value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnInSampleEndChanged(DateTimeOffset? value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnOutOfSampleStartChanged(DateTimeOffset? value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnOutOfSampleEndChanged(DateTimeOffset? value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnIsRunningChanged(bool value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnHistoricalCsvPathChanged(string? value) => RunCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRun))]
    public async Task RunAsync()
    {
        IsRunning = true;
        ProgressText = "Loading historical data...";
        ProgressValue = 0;
        try
        {
            var importer = _services.GetRequiredService<CsvHistoricalOddsImporter>();
            var snapshots = importer.Import(HistoricalCsvPath!);
            // Result lookup: caller may provide a separate CSV; backtester tolerates empty.
            var results = new Dictionary<string, EventResult>(StringComparer.OrdinalIgnoreCase);

            var cfg = new BacktestConfig
            {
                InSampleStart = InSampleStart!.Value,
                InSampleEnd = InSampleEnd!.Value,
                OutOfSampleStart = OutOfSampleStart!.Value,
                OutOfSampleEnd = OutOfSampleEnd!.Value,
                Sports = SelectedSportKeys.Select(k => new Sport(k, k)).ToList(),
                BookKeys = SelectedBookKeys.ToList(),
                Markets = new[] { MarketType.H2H, MarketType.Spread, MarketType.Total },
                MinEvPercent = MinEvPercent / 100d,
                KellyFraction = KellyFraction,
                DevigMethodKey = DevigMethodKey,
                StartingBankroll = StartingBankroll,
                RunName = RunName,
            };

            var progress = new Progress<BacktestProgress>(p =>
            {
                ProgressValue = p.Fraction * 100;
                ProgressText = $"{p.Phase}: {p.BetsPlaced} bets, bankroll ${p.Bankroll:F2}";
            });
            Report = await _backtester.RunAsync(cfg, snapshots, results, progress);
            ProgressText = "Completed";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
