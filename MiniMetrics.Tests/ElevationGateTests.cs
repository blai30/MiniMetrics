using System.Collections.Generic;
using MiniMetrics.Lib;
using Xunit;

namespace MiniMetrics.Tests;

public class ElevationGateTests
{
    private static Dictionary<string, bool> Enabled() => new() { ["cpu.temp"] = true };
    private static Dictionary<string, bool> Disabled() => new() { ["cpu.temp"] = false };

    [Fact]
    public void Relaunch_when_required_and_not_elevated()
        => Assert.True(ElevationGate.ShouldRelaunch(Enabled(), isElevated: false));

    [Fact]
    public void No_relaunch_when_required_but_already_elevated()
        => Assert.False(ElevationGate.ShouldRelaunch(Enabled(), isElevated: true));

    [Fact]
    public void No_relaunch_when_not_required_and_not_elevated()
        => Assert.False(ElevationGate.ShouldRelaunch(Disabled(), isElevated: false));

    [Fact]
    public void No_relaunch_when_not_required_and_elevated()
        => Assert.False(ElevationGate.ShouldRelaunch(Disabled(), isElevated: true));
}
