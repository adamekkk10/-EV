using Avalonia;
using Avalonia.ReactiveUI;
using QuestPDF.Infrastructure;

namespace PlusEV.UI;

/// <summary>Avalonia entry point. Keep as minimal as possible; hosting lives in <see cref="App"/>.</summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // QuestPDF community licence — required before any PDF generation.
        QuestPDF.Settings.License = LicenseType.Community;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
