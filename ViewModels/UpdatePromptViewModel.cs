namespace MiniMetrics.ViewModels;

// Display state for the update prompt window. Actionable mode (an update is available) shows the
// View release / Skip / Remind me later buttons; informational mode (up to date or failed) shows a
// single Close button. Immutable; built through the static factories.
public sealed class UpdatePromptViewModel
{
    private UpdatePromptViewModel(bool isActionable, string heading, string body, string? version, string? url)
    {
        IsActionable = isActionable;
        Heading = heading;
        Body = body;
        Version = version;
        Url = url;
    }

    public bool IsActionable { get; }

    public bool IsInformational => !IsActionable;

    public string Heading { get; }

    public string Body { get; }

    public string? Version { get; }

    public string? Url { get; }

    public static UpdatePromptViewModel ForAvailable(string latestVersion, string currentVersion, string url) =>
        new(
            true,
            "A new version is available",
            $"MiniMetrics {latestVersion} is available. You're on {currentVersion}. Open the release page to download it.",
            latestVersion,
            url);

    public static UpdatePromptViewModel ForUpToDate(string currentVersion) =>
        new(
            false,
            "You're up to date",
            $"MiniMetrics {currentVersion} is the latest version.",
            null,
            null);

    public static UpdatePromptViewModel ForFailed() =>
        new(
            false,
            "Update check failed",
            "Couldn't reach GitHub to check for updates. Please try again later.",
            null,
            null);
}
