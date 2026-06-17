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

    // Compares the running version to the latest release tag and decides whether to surface an update.
    // ShouldNotify is false when the latest version is the one the user chose to skip. An unparseable tag
    // (for example a prerelease label) is treated as no update.
    public static UpdateDecision Evaluate(Version currentVersion, string latestTag, string? skippedVersion)
    {
        if (!TryParseVersion(latestTag, out Version latest))
        {
            return new UpdateDecision(false, false, null);
        }

        bool available = latest > Normalize(currentVersion);
        bool skipped = skippedVersion is not null
            && TryParseVersion(skippedVersion, out Version skip)
            && skip == latest;

        return new UpdateDecision(available, available && !skipped, latest);
    }

    // Collapses a Version to major.minor.patch so a 4-part assembly version (1.2.0.0) compares cleanly
    // against a 3-part release tag (1.2.0).
    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);

    // Parses "v1.2.3" / "1.2.3" / "1.2" into a normalized major.minor.patch Version. Returns false for
    // anything System.Version cannot parse.
    private static bool TryParseVersion(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string trimmed = tag.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        if (!Version.TryParse(trimmed, out Version? parsed) || parsed is null)
        {
            return false;
        }

        version = Normalize(parsed);
        return true;
    }
}

// The outcome of comparing the running version to the latest release. ShouldNotify is UpdateAvailable
// minus any skip suppression.
public readonly record struct UpdateDecision(bool UpdateAvailable, bool ShouldNotify, Version? LatestVersion);
