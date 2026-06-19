namespace MiniMetrics.Models;

// Option enums for the persisted Settings shape. Each is a bare choice list with no behavior, so they
// live together next to the settings they configure rather than one per file.

public enum AppTheme
{
    System,
    Light,
    Dark
}

// Horizontal alignment of the clock widget's text.
public enum ClockAlignment
{
    Left,
    Center,
    Right
}

// How often the launch-time update check is allowed to run. EveryLaunch always checks; the rest gate
// on the time since the last successful check.
public enum UpdateCheckFrequency
{
    EveryLaunch,
    Daily,
    Weekly,
    Monthly
}
