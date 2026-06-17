using System;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMetrics.Services;

// Portable / dev update flow: delegates to the version-compare UpdateService and never applies updates in
// place, because a running single-file exe cannot replace itself. The host opens the release page instead.
public sealed class NotifyUpdateFlow : IUpdateFlow
{
    private readonly UpdateService _service;

    public NotifyUpdateFlow(UpdateService service)
    {
        _service = service;
    }

    public bool CanApplyInApp => false;

    public Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken ct = default) =>
        _service.CheckAsync(manual, ct);

    public Task ApplyAndRestartAsync() =>
        throw new InvalidOperationException("The portable update flow cannot apply updates in place.");
}
