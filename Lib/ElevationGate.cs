using System.Collections.Generic;

namespace MiniMetrics.Lib;

// The single relaunch decision: a non-elevated process must restart elevated when any
// elevation-requiring metric is enabled. Shared by Program.Main (the launch gate) and App (the
// runtime toggle) so the rule lives in exactly one place.
public static class ElevationGate
{
    public static bool ShouldRelaunch(IReadOnlyDictionary<string, bool> visibility, bool isElevated) =>
        MetricRegistry.RequiresElevation(visibility) && !isElevated;
}
