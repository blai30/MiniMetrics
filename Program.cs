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
        // CPU temperature and power are read through the PawnIO kernel driver, whose device only an
        // elevated process can open. If one of those metrics is enabled, the driver is installed, and we
        // are not elevated, relaunch elevated and let this instance exit before any window appears. A
        // declined prompt falls through and runs normally.
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
        var driver = new WindowsDriverProbe();
        if (!ElevationGate.ShouldRelaunch(settings.Visibility, elevation.IsElevated(), driver.IsInstalled()))
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
