using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlusEV.Application.Services;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.UI.ViewModels;

public sealed partial class BetTrackerViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PlusEvDbContext> _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;

    public BetTrackerViewModel(
        IDbContextFactory<PlusEvDbContext> dbFactory,
        IServiceScopeFactory scopeFactory)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
    }

    public ObservableCollection<Bet> Bets { get; } = new();

    [ObservableProperty] private Mode _selectedMode = Mode.Live;

    [RelayCommand]
    public async Task LoadAsync()
    {
        Bets.Clear();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Bets.AsNoTracking()
            .Where(b => b.Mode == SelectedMode)
            .OrderByDescending(b => b.PlacedAt)
            .Take(500)
            .ToListAsync();
        foreach (var b in rows) Bets.Add(b);
    }

    [RelayCommand]
    public async Task SettleAsync((Guid BetId, BetResult Result) arg)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var tracker = scope.ServiceProvider.GetRequiredService<BetTracker>();
        await tracker.SettleAsync(arg.BetId, arg.Result, null);
        await LoadAsync();
    }
}
