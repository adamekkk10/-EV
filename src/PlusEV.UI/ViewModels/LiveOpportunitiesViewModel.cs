using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlusEV.Application.Services;
using PlusEV.Core.Domain;

namespace PlusEV.UI.ViewModels;

/// <summary>
/// The main +EV table. Binds directly to the live <see cref="OpportunityFeed"/> and
/// applies user-tunable filters on top.
/// </summary>
public sealed partial class LiveOpportunitiesViewModel : ViewModelBase
{
    private readonly OpportunityFeed _feed;

    public LiveOpportunitiesViewModel(OpportunityFeed feed)
    {
        _feed = feed;
        _feed.Updated += (_, _) => Refresh();
        Refresh();
    }

    [ObservableProperty] private double _minEvPercent = 2d;
    [ObservableProperty] private double _minConfidence = 40d;
    [ObservableProperty] private string? _sportFilter;
    [ObservableProperty] private string? _bookFilter;
    [ObservableProperty] private bool _autoRefresh = true;
    [ObservableProperty] private DateTimeOffset _lastRefresh = DateTimeOffset.Now;

    public ObservableCollection<EvOpportunity> Visible { get; } = new();

    partial void OnMinEvPercentChanged(double _) => Refresh();
    partial void OnMinConfidenceChanged(double _) => Refresh();
    partial void OnSportFilterChanged(string? _) => Refresh();
    partial void OnBookFilterChanged(string? _) => Refresh();

    [RelayCommand]
    public void Refresh()
    {
        Visible.Clear();
        foreach (var o in _feed.Items)
        {
            if (o.EvPercent * 100d < MinEvPercent) continue;
            if (o.Confidence < MinConfidence) continue;
            if (!string.IsNullOrWhiteSpace(SportFilter) &&
                !string.Equals(o.Sport.Key, SportFilter, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(BookFilter) &&
                !string.Equals(o.Book.Key, BookFilter, StringComparison.OrdinalIgnoreCase)) continue;
            Visible.Add(o);
        }
        LastRefresh = DateTimeOffset.Now;
    }
}
