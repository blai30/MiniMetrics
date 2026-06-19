using System;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMetrics.Services;

// Orchestrates one update check: fetch the latest release, compare against the running version, and
// return a result the composition root acts on. Stamps the last-check time only on a successful fetch,
// so an offline launch retries next time instead of waiting out the cadence. A single in-flight guard
// stops an auto-check and a manual check from running at once.
public sealed class UpdateService(
    IReleaseSource source,
    Version currentVersion,
    SettingsController settings,
    Func<DateTimeOffset> nowUtc)
{
    private int _inFlight;

    public async Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0) return UpdateCheckResult.Busy();

        try
        {
            var release = await source.GetLatestAsync(ct).ConfigureAwait(false);
            if (release is null) return UpdateCheckResult.Failed();

            settings.SetLastUpdateCheck(nowUtc());

            return UpdateCheckDecision.Evaluate(
                currentVersion,
                release.TagName,
                settings.Current.SkippedUpdateVersion,
                manual,
                release.HtmlUrl,
                CurrentVersionString());
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    private string CurrentVersionString() =>
        new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build < 0 ? 0 : currentVersion.Build)
            .ToString();
}
