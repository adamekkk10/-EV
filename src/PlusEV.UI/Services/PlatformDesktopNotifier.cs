using System.Diagnostics;
using System.Runtime.InteropServices;
using PlusEV.Infrastructure.Alerts;

namespace PlusEV.UI.Services;

/// <summary>
/// Cross-platform best-effort desktop notifications.
/// Windows: msg box balloon via shell toast (falls back to no-op).
/// macOS: <c>osascript -e 'display notification ...'</c>.
/// Linux: <c>notify-send</c>.
/// </summary>
public sealed class PlatformDesktopNotifier : IDesktopNotifier
{
    public void Notify(string title, string body)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Run("notify-send", $"-a PlusEV \"{Escape(title)}\" \"{Escape(body)}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Run("osascript", $"-e 'display notification \"{Escape(body)}\" with title \"{Escape(title)}\"'");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Avalonia ships TrayIcon but lacks a generic toast API; integrate with a
                // TaskbarIcon NuGet in a later iteration. For now we write to the console
                // so the alert still surfaces somewhere.
                Console.WriteLine($"[Notification] {title}: {body}");
            }
        }
        catch { /* platform notifications are best-effort */ }
    }

    private static void Run(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi);
        p?.WaitForExit(500);
    }

    private static string Escape(string s) => s.Replace("\"", "\\\"");
}
