using System.Text.Json;
using Avalonia;

namespace CodexTokenOverlay;

internal static class Program
{
    internal static string SessionRoot { get; private set; } = SessionPathResolver.Resolve();

    [STAThread]
    public static int Main(string[] args)
    {
        SessionRoot = SessionPathResolver.Resolve(args);
        int probeIndex = Array.FindIndex(args, value => value.Equals("--probe", StringComparison.OrdinalIgnoreCase));
        if (probeIndex >= 0)
        {
            return RunProbe(args, probeIndex);
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static int RunProbe(IReadOnlyList<string> args, int probeIndex)
    {
        using TokenLogMonitor monitor = new(SessionRoot, allowNonDesktopSessions: true);
        TokenSnapshot? snapshot = monitor.Poll(forceFullScan: true);
        if (snapshot == null)
        {
            return 2;
        }

        if (probeIndex + 1 < args.Count && !args[probeIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            string outputPath = Path.GetFullPath(args[probeIndex + 1]);
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(
                outputPath,
                JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }

        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<PortableApp>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
