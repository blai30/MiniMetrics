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
        // Persist any otherwise-silent crash so a field failure can be diagnosed. Registered first so it
        // covers the whole startup sequence below.
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
        // Velopack must process any install/update/uninstall hook arguments and exit before this process
        // does anything else: it is run before the elevation gate and the single-instance mutex so a hook
        // invocation never relaunches elevated or contends for the mutex. The Add/Remove Programs uninstall
        // path runs OnBeforeUninstallFastCallback, which cannot show UI or be canceled, so it clears the
        // per-user run key and removes the autostart task non-elevated (possible because this version grants
        // the user delete rights on it). The elevated scheduled-task fallback lives in the in-app Uninstall.
        var velopackApp = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
            velopackApp = velopackApp.OnBeforeUninstallFastCallback(_ =>
            {
                if (!OperatingSystem.IsWindows()) return;
                var operations = new WindowsStartupOperations();
                operations.RemoveRunKey();

                // Non-elevated only: this FastCallback must not show UI (so no UAC) and is killed after
                // 30 seconds. Tasks created by this version are user-deletable and removed silently;
                // tasks left by older versions are admin-only and remain (documented manual cleanup).
                if (operations.TaskExists()) operations.RemoveTaskNonElevated();
            });
        velopackApp.Run();

        // CPU temperature and power are read through the PawnIO kernel driver, whose device only an
        // elevated process can open. If one of those metrics is enabled, the driver is installed, and we
        // are not elevated, relaunch elevated and let this instance exit before any window appears. A
        // declined prompt falls through and runs normally.
        if (OperatingSystem.IsWindows() && RelaunchedElevated()) return;

        // Only one instance may run at a time. Acquire the guard after the elevation gate above so the
        // non-elevated instance that relaunches itself elevated never holds the mutex: the elevated child
        // claims it cleanly. A second launch finds the mutex taken and exits before any window appears.
        using var instance = SingleInstance.Acquire();
        if (!instance.IsOnlyInstance) return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    [SupportedOSPlatform("windows")]
    private static bool RelaunchedElevated()
    {
        var settings = new SettingsStore(SettingsStore.DefaultPath).Load();
        var coordinator = new ElevationCoordinator(new WindowsElevation(), new WindowsDriverProbe());
        return coordinator.ShouldRelaunch(settings.Visibility) && coordinator.RelaunchElevated(Environment.ProcessPath!);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
