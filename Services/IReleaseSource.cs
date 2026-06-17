using System.Threading;
using System.Threading.Tasks;

namespace MiniMetrics.Services;

// A published release as seen by the update checker.
public sealed record ReleaseInfo(string TagName, string HtmlUrl);

// Fetches the latest published release. Returns null on any failure so callers never see exceptions.
public interface IReleaseSource
{
    Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct);
}
