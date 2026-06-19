using MiniMetrics.Models;
using MiniMetrics.Services;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Tests;

[TestClass]
public class MetricActivatorTests
{
    private sealed class FakeElevation : IElevation
    {
        public bool Elevated;
        public string? RelaunchedWith;
        public bool RelaunchSucceeds = true;
        public int RelaunchCalls;

        public bool IsElevated() => Elevated;

        public bool RelaunchElevated(string exePath)
        {
            RelaunchCalls++;
            RelaunchedWith = exePath;
            return RelaunchSucceeds;
        }
    }

    private sealed class FakeDriverProbe : IDriverProbe
    {
        public bool Installed;
        public bool IsInstalled() => Installed;
    }

    private const string ExePath = @"C:\MiniMetrics\MiniMetrics.exe";

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");

    private static MetricsSnapshot Snapshot() => new(
        new(34.0, null, null),
        new(12_026_124_800UL, 34_359_738_368UL),
        new(78.0, 71.0, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    private sealed class Harness
    {
        public required MetricActivator Activator { get; init; }
        public required SettingsController Controller { get; init; }
        public required FakeSaveScheduler Scheduler { get; init; }
        public required FakeElevation Elevation { get; init; }
        public required FakeStartupOperations Startup { get; init; }
        public required RecordingSensorSource Source { get; init; }
    }

    private static Harness NewHarness(
        bool elevated = false,
        bool driverInstalled = false,
        bool relaunchSucceeds = true,
        bool hasStartupManager = false,
        bool taskPresent = false,
        string? runKeyPath = null)
    {
        var scheduler = new FakeSaveScheduler();
        var controller = new SettingsController(new(), new(TempPath()), scheduler);
        var cpu = new MetricWidgetViewModel("cpu", "ram");
        var gpu = new MetricWidgetViewModel("gpu", "vram");
        cpu.BindVisibility(controller.Current.Visibility);
        gpu.BindVisibility(controller.Current.Visibility);
        cpu.ApplySnapshot(Snapshot());
        gpu.ApplySnapshot(Snapshot());
        var source = new RecordingSensorSource();
        var widgets = new WidgetCoordinator(controller, cpu, gpu, source);

        var elevation = new FakeElevation { Elevated = elevated, RelaunchSucceeds = relaunchSucceeds };
        var elevationCoordinator =
            new ElevationCoordinator(elevation, new FakeDriverProbe { Installed = driverInstalled });

        var startupOps = new FakeStartupOperations { TaskPresent = taskPresent, RunKeyPath = runKeyPath };
        var startup = new StartupManager(startupOps, ExePath);
        Func<StartupManager?> resolveStartup = hasStartupManager ? () => startup : () => null;

        var activator = new MetricActivator(widgets, elevationCoordinator, controller, resolveStartup, ExePath);

        return new()
        {
            Activator = activator,
            Controller = controller,
            Scheduler = scheduler,
            Elevation = elevation,
            Startup = startupOps,
            Source = source
        };
    }

    [TestMethod]
    public void Apply_persists_visibility_and_reconciles_devices_for_a_non_elevation_metric()
    {
        var harness = NewHarness();

        var result = harness.Activator.Apply("cpu.usage", false);

        Assert.AreEqual(MetricActivationOutcome.None, result.Outcome);
        Assert.IsFalse(harness.Controller.Current.Visibility["cpu.usage"]);
        Assert.IsTrue(harness.Source.SetActiveDevicesCount > 0);
    }

    [TestMethod]
    public void Apply_non_elevation_metric_never_relaunches_even_with_driver()
    {
        var harness = NewHarness(false, true);

        harness.Activator.Apply("cpu.usage", true);

        Assert.AreEqual(0, harness.Elevation.RelaunchCalls);
    }

    [TestMethod]
    public void Apply_relaunches_when_elevation_metric_enabled_unelevated_with_driver()
    {
        var harness = NewHarness(false, true);

        var result = harness.Activator.Apply("cpu.temp", true);

        Assert.AreEqual(MetricActivationOutcome.Relaunching, result.Outcome);
        Assert.AreEqual(ExePath, harness.Elevation.RelaunchedWith);
        // The enabled state is persisted before the relaunch so the elevated instance reads it from disk.
        Assert.IsTrue(harness.Controller.Current.Visibility["cpu.temp"]);
    }

    [TestMethod]
    public void Apply_flushes_settings_before_relaunch()
    {
        var harness = NewHarness(false, true);

        harness.Activator.Apply("cpu.temp", true);

        Assert.IsTrue(harness.Scheduler.FlushCount > 0);
    }

    [TestMethod]
    public void Apply_shows_driver_install_prompt_when_driver_missing()
    {
        var harness = NewHarness(false, false);

        var result = harness.Activator.Apply("cpu.temp", true);

        Assert.AreEqual(MetricActivationOutcome.ShowDriverInstallPrompt, result.Outcome);
        Assert.AreEqual(0, harness.Elevation.RelaunchCalls);
    }

    [TestMethod]
    public void Apply_reports_declined_when_the_uac_prompt_is_refused()
    {
        var harness = NewHarness(false, true, false);

        var result = harness.Activator.Apply("cpu.temp", true);

        Assert.AreEqual(MetricActivationOutcome.RelaunchDeclined, result.Outcome);
    }

    [TestMethod]
    public void Apply_does_nothing_elevation_related_when_already_elevated()
    {
        var harness = NewHarness(true, true);

        var result = harness.Activator.Apply("cpu.temp", true);

        Assert.AreEqual(MetricActivationOutcome.None, result.Outcome);
        Assert.AreEqual(0, harness.Elevation.RelaunchCalls);
        Assert.IsFalse(result.StartupResynced);
    }

    [TestMethod]
    public void Apply_migrates_the_elevated_task_to_a_run_key_when_the_last_elevation_metric_is_turned_off()
    {
        // Startup is on via the elevated task because an elevation metric was enabled.
        var harness = NewHarness(false, true, hasStartupManager: true, taskPresent: true);
        harness.Controller.SetMetricVisibility("cpu.temp", true);

        var result = harness.Activator.Apply("cpu.temp", false);

        Assert.AreEqual(MetricActivationOutcome.None, result.Outcome);
        Assert.IsTrue(result.StartupResynced);
        // No privileged metric remains, so the elevated task is removed and an ordinary run key takes over.
        Assert.IsTrue(result.StartupEnabled);
        Assert.AreEqual(1, harness.Startup.RemoveTaskCalls);
        Assert.IsFalse(harness.Startup.TaskPresent);
        Assert.IsNotNull(harness.Startup.RunKeyPath);
    }

    [TestMethod]
    public void Apply_keeps_the_elevated_task_when_a_second_elevation_metric_is_enabled_while_elevated()
    {
        var harness = NewHarness(true, true, hasStartupManager: true, taskPresent: true);
        harness.Controller.SetMetricVisibility("cpu.temp", true);

        var result = harness.Activator.Apply("cpu.power", true);

        Assert.AreEqual(MetricActivationOutcome.None, result.Outcome);
        Assert.IsTrue(result.StartupResynced);
        Assert.IsTrue(result.StartupEnabled);
        Assert.IsTrue(harness.Startup.TaskPresent);
        Assert.AreEqual(0, harness.Startup.RemoveTaskCalls);
    }

    [TestMethod]
    public void Apply_does_not_touch_startup_when_no_startup_manager_is_present()
    {
        var harness = NewHarness(true, true, hasStartupManager: false);
        harness.Controller.SetMetricVisibility("cpu.temp", true);

        var result = harness.Activator.Apply("cpu.power", true);

        Assert.IsFalse(result.StartupResynced);
    }

    [TestMethod]
    public void Apply_does_not_resync_startup_when_startup_is_disabled()
    {
        var harness = NewHarness(true, true, hasStartupManager: true, taskPresent: false);

        var result = harness.Activator.Apply("cpu.temp", false);

        Assert.IsFalse(result.StartupResynced);
        Assert.AreEqual(0, harness.Startup.RemoveTaskCalls);
    }
}
