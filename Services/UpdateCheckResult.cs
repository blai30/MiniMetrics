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
