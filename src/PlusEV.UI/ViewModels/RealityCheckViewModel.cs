using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PlusEV.Application.Services;
using PlusEV.Core.Domain;
using PlusEV.Core.Math;

namespace PlusEV.UI.ViewModels;

public sealed partial class RealityCheckViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RealityCheckViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    [ObservableProperty] private Mode _mode = Mode.Live;
    [ObservableProperty] private int _sampleSize;
    [ObservableProperty] private double _meanClv;
    [ObservableProperty] private double _tStatistic;
    [ObservableProperty] private double _pValue = 1d;
    [ObservableProperty] private int _requiredSampleSize;
    [ObservableProperty] private string _verdictLabel = "Insufficient data";
    [ObservableProperty] private string _verdictExplanation =
        "We need at least ~50 settled bets with recorded CLV before the t-statistic means anything.";

    [RelayCommand]
    public async Task LoadAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<RealityCheckService>();
        var snap = await service.GetAsync(Mode);
        SampleSize = snap.SampleSize;
        MeanClv = snap.MeanClv;
        TStatistic = snap.TStatistic;
        PValue = snap.PValue;
        RequiredSampleSize = snap.RequiredSampleSize;
        VerdictLabel = snap.Verdict.ToString();
        VerdictExplanation = snap.Verdict switch
        {
            RealityVerdict.InsufficientData =>
                $"Only {snap.SampleSize} observations so far; need ~50 before the signal stabilises.",
            RealityVerdict.EdgeNotSignificant =>
                $"Mean CLV {snap.MeanClv:P2} but not yet statistically distinguishable from zero (t={snap.TStatistic:F2}).",
            RealityVerdict.EdgeEmerging =>
                $"Encouraging: t-stat {snap.TStatistic:F2}. Keep betting the same way; need ~{snap.RequiredSampleSize} samples for confirmation.",
            RealityVerdict.EdgeConfirmed =>
                $"Edge confirmed: mean CLV {snap.MeanClv:P2}, t={snap.TStatistic:F2}, p={snap.PValue:F3}.",
            RealityVerdict.NegativeEdgeDetected =>
                $"Negative CLV is statistically significant (t={snap.TStatistic:F2}). Review your pipeline.",
            _ => "",
        };
    }
}
