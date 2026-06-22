using Avalonia;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using MiniMetrics.Services;
using Velopack;

namespace MiniMetrics;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack must process hook args and exit before anything else, so it runs first (the analyzer
        // also requires VelopackApp.Run() in Main): a hook invocation must never relaunch elevated or
        // contend for the mutex. OnBeforeUninstallFastCallback handles Add/Remove Programs uninstall, which
        // can't show UI or be canceled, so it clears the run key and removes the autostart task non-elevated.
        var velopackApp = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
            velopackApp = velopackApp.OnBeforeUninstallFastCallback(_ =>
            {
                if (!OperatingSystem.IsWindows()) return;
                var operations = new WindowsStartupOperations();
                operations.RemoveRunKey();

                // Non-elevated only (no UAC, killed after 30s). Only this version's user-deletable tasks
                // are removed; admin-only tasks from older versions remain (documented manual cleanup).
                if (operations.TaskExists()) operations.RemoveTaskNonElevated();
            });
        velopackApp.Run();

        // Persist otherwise-silent crashes; covers the rest of the startup sequence below.
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            CrashLog.Write("UnhandledException", eventArgs.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            CrashLog.Write("UnobservedTaskException", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        try
        {
            Run(args);
        }
        catch (Exception exception)
        {
            CrashLog.Write("Startup", exception);
            throw;
        }
    }

    private static void Run(string[] args)
    {
        // CPU temp/power need an elevated process to open the PawnIO device. If enabled with the driver
        // installed but unelevated, relaunch elevated and exit; a declined prompt falls through to run normally.
        if (OperatingSystem.IsWindows() && RelaunchedElevated()) return;

        // Acquire the single-instance guard after the elevation gate so the instance that relaunches itself
        // elevated never holds the mutex; the elevated child claims it cleanly. A second launch exits here.
        using var instance = SingleInstance.Acquire();
        if (!instance.IsOnlyInstance) return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    [SupportedOSPlatform("windows")]
    private static bool RelaunchedElevated()
    {
        var settings = new SettingsStore(SettingsStore.DefaultPath).Load();
        var coordinator = new ElevationCoordinator(new WindowsElevation(), new WindowsDriverProbe());
        return coordinator.ShouldRelaunch(settings.Visibility) &&
               coordinator.RelaunchElevated(Environment.ProcessPath!);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
