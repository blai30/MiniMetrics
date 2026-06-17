namespace MiniMetrics.Models;

// How often the launch-time update check is allowed to run. EveryLaunch always checks; the rest gate
// on the time since the last successful check.
public enum UpdateCheckFrequency
{
    EveryLaunch,
    Daily,
    Weekly,
    Monthly,
}
