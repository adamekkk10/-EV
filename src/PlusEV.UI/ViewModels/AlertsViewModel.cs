using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PlusEV.Core.Abstractions;
using PlusEV.Core.Domain;
using PlusEV.Infrastructure.Persistence;

namespace PlusEV.UI.ViewModels;

public sealed partial class AlertsViewModel : ViewModelBase
{
    private readonly IEnumerable<IAlertChannel> _channels;
    private readonly IDbContextFactory<PlusEvDbContext> _dbFactory;

    public AlertsViewModel(
        IEnumerable<IAlertChannel> channels,
        IDbContextFactory<PlusEvDbContext> dbFactory)
    {
        _channels = channels;
        _dbFactory = dbFactory;
        foreach (var c in channels) ChannelNames.Add($"{c.DisplayName} ({(c.Enabled ? "enabled" : "off")})");
    }

    public ObservableCollection<string> ChannelNames { get; } = new();
    public ObservableCollection<AlertLogEntry> RecentLog { get; } = new();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        RecentLog.Clear();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.AlertLog.AsNoTracking()
            .OrderByDescending(l => l.SentAt).Take(200).ToListAsync();
        foreach (var r in rows) RecentLog.Add(r);
    }

    [RelayCommand]
    public async Task TestChannelAsync(string channelKey)
    {
        foreach (var c in _channels)
            if (string.Equals(c.Key, channelKey, StringComparison.OrdinalIgnoreCase))
                await c.TestAsync();
        await RefreshAsync();
    }
}
