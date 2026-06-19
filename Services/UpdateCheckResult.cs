using System;
using MiniMetrics.Lib;

namespace MiniMetrics.Services;

public enum UpdateOutcome
{
    UpToDate,
    UpdateAvailable,
    Failed,
    Busy,
}

// The result of an update check. Version is the display string (no leading v); ReleaseUrl is set only
// when an update is available.
public sealed class UpdateCheckResult
{
    private UpdateCheckResult(UpdateOutcome outcome, string? version, string? releaseUrl)
    {
        Outcome = outcome;
        Version = version;
        ReleaseUrl = releaseUrl;
    }

    public UpdateOutcome Outcome { get; }
    public string? Version { get; }
    public string? ReleaseUrl { get; }

    public static UpdateCheckResult UpToDate(string version) => new(UpdateOutcome.UpToDate, version, null);
    public static UpdateCheckResult UpdateAvailable(string version, string url) => new(UpdateOutcome.UpdateAvailable, version, url);
    public static UpdateCheckResult Failed() => new(UpdateOutcome.Failed, null, null);
    public static UpdateCheckResult Busy() => new(UpdateOutcome.Busy, null, null);
}

// The single place that turns a fetched latest version into an UpdateCheckResult. Both update flows feed
// it the versions they obtained (the portable flow from a release tag, the installed flow from Velopack),
// so the "is this newer, and has the user skipped it?" rule lives in one body. Pure and I/O-free: callers
// do the fetching and the applying; this only decides the outcome.
public static class UpdateCheckDecision
{
    // currentVersion / latestTag drive the version comparison; skippedVersion suppresses an auto-check of a
    // version the user chose to skip (a manual check shows it regardless). currentVersionDisplay is the
    // string reported when no update is offered, and releaseUrl is attached when one is.
    public static UpdateCheckResult Evaluate(
        Version currentVersion,
        string latestTag,
        string? skippedVersion,
        bool manual,
        string releaseUrl,
        string currentVersionDisplay)
    {
        UpdateDecision decision = UpdatePolicy.Evaluate(currentVersion, latestTag, skippedVersion);

        if (!decision.UpdateAvailable)
        {
            return UpdateCheckResult.UpToDate(currentVersionDisplay);
        }

        // Auto-checks honor the skip; a manual check shows the update regardless, since the user
        // explicitly asked.
        if (!manual && !decision.ShouldNotify)
        {
            return UpdateCheckResult.UpToDate(currentVersionDisplay);
        }

        return UpdateCheckResult.UpdateAvailable(decision.LatestVersion!.ToString(), releaseUrl);
    }
}
