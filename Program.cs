using Avalonia;
using System;
using System.Runtime.Versioning;
using MiniMetrics.Lib;
using MiniMetrics.Services;

namespace MiniMetrics;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // CPU temperature and power need the ring0 driver, which only loads in an elevated process. If
        // one of those metrics is enabled and we are not elevated, relaunch elevated and let this
        // instance exit before any window appears. A declined prompt falls through and runs normally.
        if (OperatingSystem.IsWindows() && RelaunchedElevated())
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    [SupportedOSPlatform("windows")]
    private static bool RelaunchedElevated()
    {
        var settings = new SettingsStore(SettingsStore.DefaultPath).Load();
        var elevation = new WindowsElevation();
        if (!ElevationGate.ShouldRelaunch(settings.Visibility, elevation.IsElevated()))
        {
            return false;
        }

        return elevation.RelaunchElevated(Environment.ProcessPath!);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
