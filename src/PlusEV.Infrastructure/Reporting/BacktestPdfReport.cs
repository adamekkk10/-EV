using PlusEV.Core.Backtesting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PlusEV.Infrastructure.Reporting;

/// <summary>Renders a walk-forward backtest report to PDF.</summary>
public sealed class BacktestPdfReport : IDocument
{
    private readonly BacktestReport _report;
    public BacktestPdfReport(BacktestReport report) => _report = report;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"PlusEV Backtest Report — {_report.Config.RunName ?? _report.RunId}",
        Author = "PlusEV",
        CreationDate = _report.RanAt.UtcDateTime,
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(30);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Text("PlusEV Backtest Report").FontSize(20).SemiBold();
                col.Item().Text($"Run: {_report.Config.RunName ?? _report.RunId}");
                col.Item().Text($"Generated: {_report.RanAt:u}").FontSize(9).FontColor(Colors.Grey.Darken2);
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Spacing(10);
                col.Item().Text("Configuration").SemiBold().FontSize(14);
                col.Item().Text(DescribeConfig(_report.Config));

                col.Item().Text("In-sample").SemiBold().FontSize(14);
                col.Item().Element(e => RenderMetrics(e, _report.InSample));

                col.Item().Text("Out-of-sample").SemiBold().FontSize(14);
                col.Item().Element(e => RenderMetrics(e, _report.OutOfSample));

                if (_report.OverfittingWarning)
                {
                    col.Item().Background(Colors.Red.Lighten4).Padding(10).Column(warn =>
                    {
                        warn.Item().Text("⚠  Overfitting warning").SemiBold().FontColor(Colors.Red.Darken2);
                        warn.Item().Text($"In-sample ROI exceeds out-of-sample ROI by " +
                                         $"{_report.OverfittingGap:F2} percentage points.");
                    });
                }
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Page "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
            });
        });
    }

    private static string DescribeConfig(BacktestConfig c) =>
        $"In-sample: {c.InSampleStart:d} → {c.InSampleEnd:d}\n" +
        $"Out-of-sample: {c.OutOfSampleStart:d} → {c.OutOfSampleEnd:d}\n" +
        $"Sports: {string.Join(", ", c.Sports.Select(s => s.Title))}\n" +
        $"Books: {string.Join(", ", c.BookKeys)}\n" +
        $"Markets: {string.Join(", ", c.Markets)}\n" +
        $"EV threshold: {c.MinEvPercent:P1}  Kelly: {c.KellyFraction:F2}  Hard cap: {c.HardCapFractionOfBankroll:P1}\n" +
        $"Devigging: {c.DevigMethodKey}  Start bankroll: ${c.StartingBankroll:F2}";

    private static void RenderMetrics(IContainer container, BacktestMetrics m)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn();
                d.RelativeColumn();
            });
            Row(t, "Total bets", m.TotalBets.ToString());
            Row(t, "Win rate", $"{m.WinRate:P1}");
            Row(t, "Profit", $"${m.Profit:F2}");
            Row(t, "ROI", $"{m.RoiPercent:F2}%");
            Row(t, "Ending bankroll", $"${m.EndingBankroll:F2}");
            Row(t, "Max drawdown", $"${m.MaxDrawdown:F2}");
            Row(t, "Average CLV", $"{m.AverageClv:P2}");
            Row(t, "Sharpe", m.SharpeRatio.ToString("F2"));
            Row(t, "Longest losing streak", m.LongestLosingStreak.ToString());
        });
    }

    private static void Row(TableDescriptor t, string k, string v)
    {
        t.Cell().PaddingVertical(2).Text(k).SemiBold();
        t.Cell().PaddingVertical(2).Text(v);
    }
}
