using System;
using MiniMetrics.Models;

namespace MiniMetrics.Lib;

// Pure, Avalonia-free update scheduling and version-comparison logic. No I/O: callers fetch the release
// and hand the values in.
public static class UpdatePolicy
{
    // Whether the launch-time check should run, given the chosen cadence and the last successful check.
    // EveryLaunch and a never-checked state are always due.
    public static bool IsDue(DateTimeOffset? lastCheckUtc, UpdateCheckFrequency frequency, DateTimeOffset nowUtc)
    {
        if (frequency == UpdateCheckFrequency.EveryLaunch || lastCheckUtc is null)
        {
            return true;
        }

        TimeSpan interval = frequency switch
        {
            UpdateCheckFrequency.Daily => TimeSpan.FromDays(1),
            UpdateCheckFrequency.Weekly => TimeSpan.FromDays(7),
            UpdateCheckFrequency.Monthly => TimeSpan.FromDays(30),
            _ => TimeSpan.Zero,
        };

        return nowUtc - lastCheckUtc.Value >= interval;
    }
}
