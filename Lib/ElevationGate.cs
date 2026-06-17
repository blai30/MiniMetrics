using System.Collections.Generic;

namespace MiniMetrics.Lib;

// The single relaunch decision: a non-elevated process must restart elevated when any
// elevation-requiring metric is enabled and the driver those metrics need is actually installed.
// Shared by Program.Main (the launch gate) and App (the runtime toggle) so the rule lives in exactly
// one place.
public static class ElevationGate
{
    // Elevation only lets us open the PawnIO driver device, whose ACL admits administrators only. With
    // no driver installed there is nothing to open, so relaunching elevated would read nothing; gate
    // the relaunch on the driver being present so we do not prompt for elevation that cannot help.
    public static bool ShouldRelaunch(
        IReadOnlyDictionary<string, bool> visibility,
        bool isElevated,
        bool driverInstalled) =>
        MetricRegistry.RequiresElevation(visibility) && !isElevated && driverInstalled;
}
