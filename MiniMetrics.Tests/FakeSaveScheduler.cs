using MiniMetrics.Services;

namespace MiniMetrics.Tests;

// A scheduler with no timer, so tests can assert the debounce policy (how often a save is scheduled)
// and decide exactly when a pending save runs by calling Flush.
public sealed class FakeSaveScheduler : ISaveScheduler
{
    private Action? _pending;

    public int ScheduleCount { get; private set; }
    public int FlushCount { get; private set; }
    public bool HasPending => _pending is not null;

    public void Schedule(Action action)
    {
        _pending = action;
        ScheduleCount++;
    }

    public void Flush()
    {
        FlushCount++;
        var pending = _pending;
        _pending = null;
        pending?.Invoke();
    }
}
