using System;
using System.Linq;
using MiniMetrics.Lib;
using MiniMetrics.Services;

namespace MiniMetrics.ViewModels;

// What enabling or disabling a metric implies for the rest of the app, once the change has been
// persisted and the widget re-rendered. App renders the outcome; it does not recompute the decision.
public enum MetricActivationOutcome
{
    // Nothing elevation-related to render. Startup registration may still have been reconciled to a
    // lower elevation need; see MetricActivationResult.StartupResynced.
    None,

    // The metric needs the PawnIO driver and it is missing, so elevation alone cannot read it: show the
    // one-time driver install prompt. The metric stays enabled and renders a placeholder until then.
    ShowDriverInstallPrompt,

    // A relaunch into an elevated copy has started: shut the current (unelevated) instance down.
    Relaunching,

    // The relaunch UAC prompt was declined: put the metric back to off.
    RelaunchDeclined,
}

// The outcome of applying one metric visibility change, plus whether startup registration was
// reconciled so the caller can reflect the new state in the tray.
public readonly record struct MetricActivationResult(
    MetricActivationOutcome Outcome,
    bool StartupResynced,
    bool StartupEnabled)
{
    public static readonly MetricActivationResult None =
        new(MetricActivationOutcome.None, false, false);

    public static readonly MetricActivationResult DriverInstallPrompt =
        new(MetricActivationOutcome.ShowDriverInstallPrompt, false, false);

    public static readonly MetricActivationResult Relaunching =
        new(MetricActivationOutcome.Relaunching, false, false);

    public static readonly MetricActivationResult RelaunchDeclined =
        new(MetricActivationOutcome.RelaunchDeclined, false, false);

    public static MetricActivationResult Resynced(bool startupEnabled) =>
        new(MetricActivationOutcome.None, true, startupEnabled);
}

// Owns the full sequence a metric visibility change implies: persist + re-render + device reconcile
// (through the widget coordinator), then, for an elevation-flagged metric, the elevation decision and
// its follow-through (relaunch, driver prompt, or reconciling the elevated startup task). App used to
// run this inline across four collaborators; concentrating it here gives the sequence one place to live
// and one interface to test, and leaves App only to render the returned outcome.
public sealed class MetricActivator
{
    private readonly WidgetCoordinator _widgets;
    private readonly ElevationCoordinator _elevation;
    private readonly SettingsController _settings;
    private readonly Func<StartupManager?> _startupManager;
    private readonly string _exePath;

    public MetricActivator(
        WidgetCoordinator widgets,
        ElevationCoordinator elevation,
        SettingsController settings,
        Func<StartupManager?> startupManager,
        string exePath)
    {
        _widgets = widgets;
        _elevation = elevation;
        _settings = settings;
        _startupManager = startupManager;
        _exePath = exePath;
    }

    // Applies a metric visibility change and returns what the app must render. Persisting, re-rendering,
    // and reconciling polled devices always happen first, as one step, so render, polling, and saved
    // state cannot drift apart.
    public MetricActivationResult Apply(string key, bool visible)
    {
        _widgets.SetMetricVisibility(key, visible);

        bool isElevationMetric = MetricRegistry.All
            .Any(entry => entry.Key == key && entry.RequiresElevation);
        if (!isElevationMetric)
        {
            return MetricActivationResult.None;
        }

        switch (_elevation.DecideMetricEnable(key, visible))
        {
            case MetricEnableAction.DriverInstallPrompt:
                return MetricActivationResult.DriverInstallPrompt;

            case MetricEnableAction.Relaunch:
                // Settings were just persisted, so the elevated instance reads the enabled state from
                // disk and reconciles startup registration itself, keeping it to one UAC prompt total.
                _settings.Flush();
                return _elevation.RelaunchElevated(_exePath)
                    ? MetricActivationResult.Relaunching
                    : MetricActivationResult.RelaunchDeclined;
        }

        // None: an elevation metric was turned off, or turned on while already elevated. Turning one on
        // while unelevated takes the relaunch path above and never reaches here, so this only ever keeps
        // or reduces the elevation requirement. A scheduled task that is no longer needed is removed even
        // while unelevated (StartupManager.Sync tries a non-elevated delete first).
        StartupManager? startup = _startupManager();
        if (startup is not null && startup.IsEnabled())
        {
            startup.Sync(true, _elevation.RequiresElevation(_settings.Current.Visibility));
            return MetricActivationResult.Resynced(startup.IsEnabled());
        }

        return MetricActivationResult.None;
    }
}
