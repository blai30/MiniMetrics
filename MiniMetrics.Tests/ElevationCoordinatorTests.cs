using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class ElevationCoordinatorTests
{
    private sealed class FakeElevation : IElevation
    {
        public bool Elevated;
        public string? RelaunchedWith;
        public bool RelaunchSucceeds = true;

        public bool IsElevated() => Elevated;

        public bool RelaunchElevated(string exePath)
        {
            RelaunchedWith = exePath;
            return RelaunchSucceeds;
        }
    }

    private sealed class FakeDriverProbe : IDriverProbe
    {
        public bool Installed;
        public bool IsInstalled() => Installed;
    }

    private static Dictionary<string, bool> Enabled() => new() { ["cpu.temp"] = true };
    private static Dictionary<string, bool> Disabled() => new() { ["cpu.temp"] = false };

    private static ElevationCoordinator Build(bool elevated, bool driverInstalled) =>
        new(new FakeElevation { Elevated = elevated }, new FakeDriverProbe { Installed = driverInstalled });

    // ShouldRelaunch mirrors ElevationGate's branch coverage through the coordinator's seams.

    [TestMethod]
    public void ShouldRelaunch_when_required_not_elevated_and_driver_installed()
        => Assert.IsTrue(Build(false, true).ShouldRelaunch(Enabled()));

    [TestMethod]
    public void No_relaunch_when_required_and_not_elevated_but_driver_missing()
        => Assert.IsFalse(Build(false, false).ShouldRelaunch(Enabled()));

    [TestMethod]
    public void No_relaunch_when_required_but_already_elevated()
        => Assert.IsFalse(Build(true, true).ShouldRelaunch(Enabled()));

    [TestMethod]
    public void No_relaunch_when_not_required_and_not_elevated()
        => Assert.IsFalse(Build(false, true).ShouldRelaunch(Disabled()));

    [TestMethod]
    public void No_relaunch_when_not_required_and_elevated()
        => Assert.IsFalse(Build(true, true).ShouldRelaunch(Disabled()));

    // NeedsDriverInstallPrompt: required and driver missing.

    [TestMethod]
    public void NeedsDriverInstallPrompt_when_required_and_driver_missing()
        => Assert.IsTrue(Build(false, false).NeedsDriverInstallPrompt(Enabled()));

    [TestMethod]
    public void No_driver_prompt_when_required_and_driver_installed()
        => Assert.IsFalse(Build(false, true).NeedsDriverInstallPrompt(Enabled()));

    [TestMethod]
    public void No_driver_prompt_when_not_required()
        => Assert.IsFalse(Build(false, false).NeedsDriverInstallPrompt(Disabled()));

    // DecideMetricEnable: only an elevation-flagged metric turned on while not elevated triggers an
    // action, and the action depends on whether the driver is present.

    [TestMethod]
    public void DecideMetricEnable_relaunch_when_elevation_metric_on_unelevated_driver_installed()
        => Assert.AreEqual(
            MetricEnableAction.Relaunch,
            Build(false, true).DecideMetricEnable("cpu.temp", true));

    [TestMethod]
    public void DecideMetricEnable_driver_prompt_when_elevation_metric_on_unelevated_driver_missing()
        => Assert.AreEqual(
            MetricEnableAction.DriverInstallPrompt,
            Build(false, false).DecideMetricEnable("cpu.temp", true));

    [TestMethod]
    public void DecideMetricEnable_none_when_elevation_metric_on_but_already_elevated()
        => Assert.AreEqual(
            MetricEnableAction.None,
            Build(true, true).DecideMetricEnable("cpu.temp", true));

    [TestMethod]
    public void DecideMetricEnable_none_when_elevation_metric_turned_off()
        => Assert.AreEqual(
            MetricEnableAction.None,
            Build(false, true).DecideMetricEnable("cpu.temp", false));

    [TestMethod]
    public void DecideMetricEnable_none_for_non_elevation_metric()
        => Assert.AreEqual(
            MetricEnableAction.None,
            Build(false, true).DecideMetricEnable("cpu.usage", true));

    [TestMethod]
    public void RequiresElevation_reflects_visibility()
    {
        var coordinator = Build(false, true);
        Assert.IsTrue(coordinator.RequiresElevation(Enabled()));
        Assert.IsFalse(coordinator.RequiresElevation(Disabled()));
    }
}
