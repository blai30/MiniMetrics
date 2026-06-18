using System.Collections.Generic;
using MiniMetrics.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        => Assert.IsTrue(Build(elevated: false, driverInstalled: true).ShouldRelaunch(Enabled()));

    [TestMethod]
    public void No_relaunch_when_required_and_not_elevated_but_driver_missing()
        => Assert.IsFalse(Build(elevated: false, driverInstalled: false).ShouldRelaunch(Enabled()));

    [TestMethod]
    public void No_relaunch_when_required_but_already_elevated()
        => Assert.IsFalse(Build(elevated: true, driverInstalled: true).ShouldRelaunch(Enabled()));

    [TestMethod]
    public void No_relaunch_when_not_required_and_not_elevated()
        => Assert.IsFalse(Build(elevated: false, driverInstalled: true).ShouldRelaunch(Disabled()));

    [TestMethod]
    public void No_relaunch_when_not_required_and_elevated()
        => Assert.IsFalse(Build(elevated: true, driverInstalled: true).ShouldRelaunch(Disabled()));

    // NeedsDriverInstallPrompt: required and driver missing.

    [TestMethod]
    public void NeedsDriverInstallPrompt_when_required_and_driver_missing()
        => Assert.IsTrue(Build(elevated: false, driverInstalled: false).NeedsDriverInstallPrompt(Enabled()));

    [TestMethod]
    public void No_driver_prompt_when_required_and_driver_installed()
        => Assert.IsFalse(Build(elevated: false, driverInstalled: true).NeedsDriverInstallPrompt(Enabled()));

    [TestMethod]
    public void No_driver_prompt_when_not_required()
        => Assert.IsFalse(Build(elevated: false, driverInstalled: false).NeedsDriverInstallPrompt(Disabled()));

    // DecideMetricEnable: only an elevation-flagged metric turned on while not elevated triggers an
    // action, and the action depends on whether the driver is present.

    [TestMethod]
    public void DecideMetricEnable_relaunch_when_elevation_metric_on_unelevated_driver_installed()
        => Assert.AreEqual(
            MetricEnableAction.Relaunch,
            Build(elevated: false, driverInstalled: true).DecideMetricEnable("cpu.temp", visible: true));

    [TestMethod]
    public void DecideMetricEnable_driver_prompt_when_elevation_metric_on_unelevated_driver_missing()
        => Assert.AreEqual(
            MetricEnableAction.DriverInstallPrompt,
            Build(elevated: false, driverInstalled: false).DecideMetricEnable("cpu.temp", visible: true));

    [TestMethod]
    public void DecideMetricEnable_none_when_elevation_metric_on_but_already_elevated()
        => Assert.AreEqual(
            MetricEnableAction.None,
            Build(elevated: true, driverInstalled: true).DecideMetricEnable("cpu.temp", visible: true));

    [TestMethod]
    public void DecideMetricEnable_none_when_elevation_metric_turned_off()
        => Assert.AreEqual(
            MetricEnableAction.None,
            Build(elevated: false, driverInstalled: true).DecideMetricEnable("cpu.temp", visible: false));

    [TestMethod]
    public void DecideMetricEnable_none_for_non_elevation_metric()
        => Assert.AreEqual(
            MetricEnableAction.None,
            Build(elevated: false, driverInstalled: true).DecideMetricEnable("cpu.usage", visible: true));

    [TestMethod]
    public void RequiresElevation_reflects_visibility()
    {
        var coordinator = Build(elevated: false, driverInstalled: true);
        Assert.IsTrue(coordinator.RequiresElevation(Enabled()));
        Assert.IsFalse(coordinator.RequiresElevation(Disabled()));
    }
}
