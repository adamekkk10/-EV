using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.UI.ViewModels;

public sealed partial class LineHistoryViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PlusEvDbContext> _dbFactory;

    public LineHistoryViewModel(IDbContextFactory<PlusEvDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public ObservableCollection<string> EventIds { get; } = new();
    public ObservableCollection<OddsHistoryPoint> Points { get; } = new();

    [ObservableProperty] private string? _selectedEventId;
    [ObservableProperty] private MarketType _selectedMarket = MarketType.H2H;
    [ObservableProperty] private double _averageLatencyMs;

    [RelayCommand]
    public async Task LoadEventsAsync()
    {
        EventIds.Clear();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var ids = await db.OddsHistory.AsNoTracking()
            .OrderByDescending(h => h.FetchedAt)
            .Select(h => h.EventId)
            .Distinct()
            .Take(200)
            .ToListAsync();
        foreach (var id in ids) EventIds.Add(id);
    }

    [RelayCommand]
    public async Task LoadPointsAsync()
    {
        Points.Clear();
        if (string.IsNullOrWhiteSpace(SelectedEventId)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.OddsHistory.AsNoTracking()
            .Where(h => h.EventId == SelectedEventId && h.Market == SelectedMarket)
            .OrderBy(h => h.FetchedAt)
            .Take(5000)
            .ToListAsync();
        foreach (var r in rows) Points.Add(r);
        AverageLatencyMs = rows.Count == 0 ? 0 : rows.Average(r => r.FetchLatencyMs);
    }
}
