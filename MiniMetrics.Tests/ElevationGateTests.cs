using System.Collections.Generic;
using MiniMetrics.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class ElevationGateTests
{
    private static Dictionary<string, bool> Enabled() => new() { ["cpu.temp"] = true };
    private static Dictionary<string, bool> Disabled() => new() { ["cpu.temp"] = false };

    [TestMethod]
    public void Relaunch_when_required_not_elevated_and_driver_installed()
        => Assert.IsTrue(ElevationGate.ShouldRelaunch(Enabled(), isElevated: false, driverInstalled: true));

    // Elevation only lets us open the PawnIO device; with no driver installed there is nothing to open,
    // so relaunching elevated would change nothing.
    [TestMethod]
    public void No_relaunch_when_required_and_not_elevated_but_driver_missing()
        => Assert.IsFalse(ElevationGate.ShouldRelaunch(Enabled(), isElevated: false, driverInstalled: false));

    [TestMethod]
    public void No_relaunch_when_required_but_already_elevated()
        => Assert.IsFalse(ElevationGate.ShouldRelaunch(Enabled(), isElevated: true, driverInstalled: true));

    [TestMethod]
    public void No_relaunch_when_not_required_and_not_elevated()
        => Assert.IsFalse(ElevationGate.ShouldRelaunch(Disabled(), isElevated: false, driverInstalled: true));

    [TestMethod]
    public void No_relaunch_when_not_required_and_elevated()
        => Assert.IsFalse(ElevationGate.ShouldRelaunch(Disabled(), isElevated: true, driverInstalled: true));
}
