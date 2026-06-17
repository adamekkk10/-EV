using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlusEV.Application.Services;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.UI.ViewModels;

public sealed partial class BankrollViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PlusEvDbContext> _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;

    public BankrollViewModel(
        IDbContextFactory<PlusEvDbContext> dbFactory,
        IServiceScopeFactory scopeFactory)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
    }

    public ObservableCollection<BankrollEntry> Entries { get; } = new();

    [ObservableProperty] private Mode _selectedMode = Mode.Live;
    [ObservableProperty] private decimal _currentBalance;
    [ObservableProperty] private decimal _totalProfit;
    [ObservableProperty] private double _roiPercent;
    [ObservableProperty] private double _hitRate;
    [ObservableProperty] private double _averageClv;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var bankroll = scope.ServiceProvider.GetRequiredService<BankrollService>();
        CurrentBalance = await bankroll.GetCurrentBalanceAsync(SelectedMode);

        await using var db = await _dbFactory.CreateDbContextAsync();
        Entries.Clear();
        var rows = await db.BankrollEntries.AsNoTracking()
            .Where(e => e.Mode == SelectedMode)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync();
        foreach (var e in rows) Entries.Add(e);

        var settled = await db.Bets.AsNoTracking()
            .Where(b => b.Mode == SelectedMode && b.Result != BetResult.Pending)
            .ToListAsync();
        TotalProfit = settled.Sum(b => b.ProfitOrLoss);
        var totalStake = settled.Sum(b => b.Stake);
        RoiPercent = totalStake == 0m ? 0d : (double)(TotalProfit / totalStake) * 100d;
        var decided = settled.Count(b => b.Result == BetResult.Won || b.Result == BetResult.Lost);
        HitRate = decided == 0 ? 0d : (double)settled.Count(b => b.Result == BetResult.Won) / decided;
        var clvs = settled.Where(b => b.Clv.HasValue).Select(b => b.Clv!.Value).ToList();
        AverageClv = clvs.Count == 0 ? 0d : clvs.Average();
    }
}
