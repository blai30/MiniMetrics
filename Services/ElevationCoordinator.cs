using System.Collections.Generic;
using System.Linq;
using MiniMetrics.Lib;

namespace MiniMetrics.Services;

// The action enabling a metric implies, given the current elevation and driver state.
public enum MetricEnableAction
{
    // Nothing elevation-related to do: either the metric is not elevation-flagged, or it is being
    // turned off, or the process is already elevated.
    None,

    // The metric needs elevation and the driver is present: restart the app elevated.
    Relaunch,

    // The metric needs elevation but PawnIO is missing, so elevation alone cannot read it: point the
    // user at the driver installer instead of prompting for elevation that cannot help.
    DriverInstallPrompt
}

// Owns the single elevation predicate and the decisions derived from it, so Program (the launch gate)
// and App (the runtime toggle, the startup-sync, and the launch-time driver prompt) ask one module
// instead of each recombining MetricRegistry.RequiresElevation + IElevation.IsElevated +
// IDriverProbe.IsInstalled by hand. The relaunch rule itself still lives in the pure ElevationGate,
// which this delegates to, so it stays unit-testable on its own.
public sealed class ElevationCoordinator(IElevation elevation, IDriverProbe driverProbe)
{
    // True when the current process holds an administrator token.
    public bool IsElevated() => elevation.IsElevated();

    // Some metrics are read through the PawnIO driver, whose device only an elevated process can open;
    // elevation is required while any such metric is visible.
    public bool RequiresElevation(IReadOnlyDictionary<string, bool> visibility) =>
        MetricRegistry.RequiresElevation(visibility);

    // The launch gate: a non-elevated process must restart elevated when an elevation-requiring metric
    // is enabled and the driver those metrics need is installed.
    public bool ShouldRelaunch(IReadOnlyDictionary<string, bool> visibility) =>
        ElevationGate.ShouldRelaunch(visibility, elevation.IsElevated(), driverProbe.IsInstalled());

    // A driver-backed metric is enabled but PawnIO is missing, so the launch gate did not relaunch
    // (elevation alone cannot read the sensors). True means the one-time install step should be shown.
    public bool NeedsDriverInstallPrompt(IReadOnlyDictionary<string, bool> visibility) =>
        RequiresElevation(visibility) && !driverProbe.IsInstalled();

    // What turning a metric on (or off) implies right now. Mirrors the launch gate: only an
    // elevation-flagged metric being turned on while not elevated triggers an action, and that action
    // is a relaunch when the driver is present or the install prompt when it is missing.
    public MetricEnableAction DecideMetricEnable(string key, bool visible)
    {
        if (!visible || elevation.IsElevated()) return MetricEnableAction.None;

        bool isElevationMetric = MetricRegistry.All
            .Any(entry => entry.Key == key && entry.RequiresElevation);
        if (!isElevationMetric) return MetricEnableAction.None;

        return driverProbe.IsInstalled()
            ? MetricEnableAction.Relaunch
            : MetricEnableAction.DriverInstallPrompt;
    }

    // Starts a new elevated copy of the app through the UAC prompt. Returns false if the prompt was
    // declined or the relaunch could not start.
    public bool RelaunchElevated(string exePath) => elevation.RelaunchElevated(exePath);
}
