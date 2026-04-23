using System.Collections.ObjectModel;
using PlusEV.Core.Domain;

namespace PlusEV.Application.Services;

/// <summary>
/// Process-wide in-memory feed of currently-live opportunities. UI viewmodels and the
/// alerts queue subscribe. Access from the UI thread only; background services post via
/// <see cref="Replace"/> atomically to keep readers safe.
/// </summary>
public sealed class OpportunityFeed
{
    private readonly object _lock = new();
    public ObservableCollection<EvOpportunity> Items { get; } = new();
    public ObservableCollection<ArbitrageOpportunity> Arbitrages { get; } = new();
    public ObservableCollection<MiddleOpportunity> Middles { get; } = new();

    public event EventHandler? Updated;

    public void Replace(
        IEnumerable<EvOpportunity> evs,
        IEnumerable<ArbitrageOpportunity> arbs,
        IEnumerable<MiddleOpportunity> middles)
    {
        lock (_lock)
        {
            Items.Clear();
            foreach (var o in evs.OrderByDescending(o => o.EvPercent)) Items.Add(o);
            Arbitrages.Clear();
            foreach (var a in arbs.OrderByDescending(a => a.GuaranteedReturnPercent)) Arbitrages.Add(a);
            Middles.Clear();
            foreach (var m in middles.OrderByDescending(m => m.MiddleWidth)) Middles.Add(m);
        }
        Updated?.Invoke(this, EventArgs.Empty);
    }
}
