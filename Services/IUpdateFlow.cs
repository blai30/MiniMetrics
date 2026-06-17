using System.Threading;
using System.Threading.Tasks;

namespace MiniMetrics.Services;

// One update path the composition root drives without caring which mode it is in. The Velopack-backed
// flow applies updates in place and restarts; the portable flow only links the user to the release page.
public interface IUpdateFlow
{
    // True when ApplyAndRestartAsync can install the pending update in place (a Velopack install). False
    // for the portable flow, whose only action is opening the release page.
    bool CanApplyInApp { get; }

    // Checks for an update. manual=true forces an informational result (up to date or failed) so a manual
    // click never feels dead; an auto-check returns UpToDate when nothing is offered.
    Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken ct = default);

    // Downloads if needed, applies the pending update, and restarts. Only valid when CanApplyInApp.
    Task ApplyAndRestartAsync();
}
