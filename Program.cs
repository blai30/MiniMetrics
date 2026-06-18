using Avalonia;
using System;
using System.Runtime.Versioning;
using MiniMetrics.Lib;
using MiniMetrics.Services;
using Velopack;

namespace MiniMetrics;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack must process any install/update/uninstall hook arguments and exit before this process
        // does anything else: it is run before the elevation gate and the single-instance mutex so a hook
        // invocation never relaunches elevated or contends for the mutex. The Add/Remove Programs uninstall
        // path runs OnBeforeUninstallFastCallback, which cannot show UI or be canceled, so it only clears
        // the per-user run key; the elevated scheduled task and the abortable flow live in the in-app
        // Uninstall command.
        var velopackApp = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
        {
            velopackApp = velopackApp.OnBeforeUninstallFastCallback(_ =>
            {
                if (OperatingSystem.IsWindows())
                {
                    new WindowsStartupOperations().RemoveRunKey();
                }
            });
        }
        velopackApp.Run();

        // CPU temperature and power are read through the PawnIO kernel driver, whose device only an
        // elevated process can open. If one of those metrics is enabled, the driver is installed, and we
        // are not elevated, relaunch elevated and let this instance exit before any window appears. A
        // declined prompt falls through and runs normally.
        if (OperatingSystem.IsWindows() && RelaunchedElevated())
        {
            return;
        }

        // Only one instance may run at a time. Acquire the guard after the elevation gate above so the
        // non-elevated instance that relaunches itself elevated never holds the mutex: the elevated child
        // claims it cleanly. A second launch finds the mutex taken and exits before any window appears.
        using var instance = SingleInstance.Acquire();
        if (!instance.IsOnlyInstance)
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
