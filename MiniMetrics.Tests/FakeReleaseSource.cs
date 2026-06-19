using MiniMetrics.Services;

namespace MiniMetrics.Tests;

// Returns a preset release (or null) and counts calls.
internal sealed class FakeReleaseSource : IReleaseSource
{
    public ReleaseInfo? Release;
    public int Calls;

    public Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(Release);
    }
}

// Blocks until the test completes the gate, so an in-flight check can be observed.
internal sealed class GatedReleaseSource : IReleaseSource
{
    public readonly TaskCompletionSource<ReleaseInfo?> Gate = new();

    public Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct) => Gate.Task;
}
