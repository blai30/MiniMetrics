using System;
using System.Threading;
using System.Threading.Tasks;
using MiniMetrics.Lib;

namespace MiniMetrics.Services;

// Orchestrates one update check: fetch the latest release, compare against the running version, and
// return a result the composition root acts on. Stamps the last-check time only on a successful fetch,
// so an offline launch retries next time instead of waiting out the cadence. A single in-flight guard
// stops an auto-check and a manual check from running at once.
public sealed class UpdateService
{
    private readonly IReleaseSource _source;
    private readonly Version _currentVersion;
    private readonly SettingsController _settings;
    private readonly Func<DateTimeOffset> _nowUtc;
    private int _inFlight;

    public UpdateService(IReleaseSource source, Version currentVersion, SettingsController settings, Func<DateTimeOffset> nowUtc)
    {
        _source = source;
        _currentVersion = currentVersion;
        _settings = settings;
        _nowUtc = nowUtc;
    }

    public async Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
        {
            return UpdateCheckResult.Busy();
        }

        try
        {
            ReleaseInfo? release = await _source.GetLatestAsync(ct).ConfigureAwait(false);
            if (release is null)
            {
                return UpdateCheckResult.Failed();
            }

            _settings.SetLastUpdateCheck(_nowUtc());

            UpdateDecision decision = UpdatePolicy.Evaluate(
                _currentVersion, release.TagName, _settings.Current.SkippedUpdateVersion);

            if (!decision.UpdateAvailable)
            {
                return UpdateCheckResult.UpToDate(CurrentVersionString());
            }

            // Auto-checks honor the skip; a manual check shows the update regardless, since the user
            // explicitly asked.
            if (!manual && !decision.ShouldNotify)
            {
                return UpdateCheckResult.UpToDate(CurrentVersionString());
            }

            return UpdateCheckResult.UpdateAvailable(decision.LatestVersion!.ToString(), release.HtmlUrl);
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    private string CurrentVersionString() =>
        new Version(_currentVersion.Major, _currentVersion.Minor, _currentVersion.Build < 0 ? 0 : _currentVersion.Build).ToString();
}
