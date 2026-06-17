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
    public void Relaunch_when_required_and_not_elevated()
        => Assert.IsTrue(ElevationGate.ShouldRelaunch(Enabled(), isElevated: false));

    [TestMethod]
    public void No_relaunch_when_required_but_already_elevated()
        => Assert.IsFalse(ElevationGate.ShouldRelaunch(Enabled(), isElevated: true));

    [TestMethod]
    public void No_relaunch_when_not_required_and_not_elevated()
        => Assert.IsFalse(ElevationGate.ShouldRelaunch(Disabled(), isElevated: false));

    [TestMethod]
    public void No_relaunch_when_not_required_and_elevated()
        => Assert.IsFalse(ElevationGate.ShouldRelaunch(Disabled(), isElevated: true));
}
