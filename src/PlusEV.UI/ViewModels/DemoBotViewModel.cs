using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PlusEV.Application.Demo;
using PlusEV.Application.Services;
using PlusEV.Core.Domain;
using PlusEV.Core.Options;

namespace PlusEV.UI.ViewModels;

/// <summary>Demo Bot control panel viewmodel.</summary>
public sealed partial class DemoBotViewModel : ViewModelBase
{
    private readonly DemoBot _bot;
    private readonly IServiceScopeFactory _scopeFactory;

    public DemoBotViewModel(DemoBot bot, IServiceScopeFactory scopeFactory)
    {
        _bot = bot;
        _scopeFactory = scopeFactory;
        Activity = bot.Activity;
        _bot.BankrollChanged += (_, balance) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentBankroll = balance);
        };
    }

    public ObservableCollection<DemoActivityEntry> Activity { get; }

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private decimal _currentBankroll;
    [ObservableProperty] private decimal _startingBankroll = 1000m;
    [ObservableProperty] private string _aggressiveness = nameof(DemoBotAggressiveness.Balanced);
    [ObservableProperty] private double _evThreshold = 2.5d;
    [ObservableProperty] private double _kellyFraction = 0.25d;
    [ObservableProperty] private double _hardCap = 0.02d;

    [RelayCommand]
    public async Task StartAsync()
    {
        await _bot.StartAsync(StartingBankroll);
        IsRunning = _bot.IsRunning;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var bankroll = scope.ServiceProvider.GetRequiredService<BankrollService>();
        CurrentBankroll = await bankroll.GetCurrentBalanceAsync(Mode.Demo);
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        await _bot.StopAsync();
        IsRunning = _bot.IsRunning;
    }

    [RelayCommand]
    public async Task ResetAsync()
    {
        await _bot.ResetAsync(StartingBankroll);
        CurrentBankroll = StartingBankroll;
    }
}
