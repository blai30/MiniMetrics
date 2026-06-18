using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;

namespace MiniMetrics.Services;

// Installed (Velopack) update flow. Checks the GitHub feed for the installed channel via UpdateManager,
// then applies the update in place and restarts. Holds the pending UpdateInfo between the check and the
// apply. The reported "current version" comes from Velopack so it matches the installed package.
public sealed class VelopackUpdateFlow : IUpdateFlow
{
    private const string ReleasePageUrl = "https://github.com/blai30/MiniMetrics/releases/latest";

    private readonly UpdateManager _manager;
    private readonly SettingsController _settings;
    private readonly Func<DateTimeOffset> _nowUtc;
    private UpdateInfo? _pending;

    public VelopackUpdateFlow(UpdateManager manager, SettingsController settings, Func<DateTimeOffset> nowUtc)
    {
        _manager = manager;
        _settings = settings;
        _nowUtc = nowUtc;
    }

    public bool CanApplyInApp => true;

    public async Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken ct = default)
    {
        UpdateInfo? info;
        try
        {
            info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Any network or feed error collapses to a failure result, matching the portable flow.
            return UpdateCheckResult.Failed();
        }

        _settings.SetLastUpdateCheck(_nowUtc());

        if (info is null)
        {
            return UpdateCheckResult.UpToDate(CurrentVersionString());
        }

        string version = info.TargetFullRelease.Version.ToString();

        // Auto-checks honor the skipped version; a manual check surfaces it regardless.
        if (!manual && version == _settings.Current.SkippedUpdateVersion)
        {
            return UpdateCheckResult.UpToDate(CurrentVersionString());
        }

        _pending = info;
        return UpdateCheckResult.UpdateAvailable(version, ReleasePageUrl);
    }

    public async Task ApplyAndRestartAsync()
    {
        if (_pending is null)
        {
            return;
        }

        // DownloadUpdatesAsync(UpdateInfo, Action<int>? progress, CancellationToken) -- pass null progress.
        await _manager.DownloadUpdatesAsync(_pending, null, CancellationToken.None).ConfigureAwait(false);
        // ApplyUpdatesAndRestart takes VelopackAsset, not UpdateInfo, in 0.0.1298.
        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease, []);
    }

    private string CurrentVersionString() => _manager.CurrentVersion?.ToString() ?? "0.0.0";
}
