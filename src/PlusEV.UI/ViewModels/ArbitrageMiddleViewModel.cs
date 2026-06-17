using System.Collections.ObjectModel;
using PlusEV.Application.Services;
using PlusEV.Core.Domain;

namespace PlusEV.UI.ViewModels;

public sealed partial class ArbitrageMiddleViewModel : ViewModelBase
{
    public ArbitrageMiddleViewModel(OpportunityFeed feed)
    {
        Arbitrages = feed.Arbitrages;
        Middles = feed.Middles;
    }

    public ObservableCollection<ArbitrageOpportunity> Arbitrages { get; }
    public ObservableCollection<MiddleOpportunity> Middles { get; }
}
