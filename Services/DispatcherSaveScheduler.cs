using System;
using Avalonia.Threading;

namespace MiniMetrics.Services;

// Debounces saves on the UI thread with a DispatcherTimer. The latest scheduled action wins, so a
// drag that fires hundreds of changes results in a single write once the gesture settles.
public sealed class DispatcherSaveScheduler : ISaveScheduler
{
    private readonly DispatcherTimer _timer;
    private Action? _pending;

    public DispatcherSaveScheduler(TimeSpan delay)
    {
        _timer = new() { Interval = delay };
        _timer.Tick += (_, _) => Flush();
    }

    public void Schedule(Action action)
    {
        _pending = action;
        _timer.Stop();
        _timer.Start();
    }

    public void Flush()
    {
        _timer.Stop();
        var pending = _pending;
        _pending = null;
        pending?.Invoke();
    }
}
