using System;
using System.Threading;
using System.Threading.Tasks;
using MiniMetrics.Models;

namespace MiniMetrics.Services;

public sealed class MetricsPoller : IDisposable
{
    private readonly ISensorSource _source;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event Action<MetricsSnapshot>? SnapshotReady;

    public MetricsPoller(ISensorSource source, TimeSpan interval)
    {
        _source = source;
        _interval = interval;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();

        // Start the loop is invoked from the UI thread, which carries Avalonia's SynchronizationContext.
        // Task.Run hops to the thread pool so the loop (and every sensor read) runs off the UI thread;
        // otherwise the await continuation would post each read back to the UI thread and stutter drags.
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            do
            {
                Emit();
            }
            while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException)
        {
            // Cancellation on Dispose ends the loop cleanly.
        }
    }

    private void Emit()
    {
        try
        {
            MetricsSnapshot snapshot = _source.Read();
            SnapshotReady?.Invoke(snapshot);
        }
        catch
        {
            // A transient sensor read failure must not kill the loop; the next tick retries.
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();

        // Wait for any in-flight read to finish before returning. The owner disposes the sensor source
        // right after this, and closing LibreHardwareMonitor (and its kernel driver) while a read is
        // still running faults natively, which is the crash seen on Windows shutdown. The wait is bounded
        // so a hung read cannot stall shutdown indefinitely. Safe to wait from the UI thread: the loop
        // only ever posts to the dispatcher, which does not block.
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The loop never surfaces faults (Emit swallows read errors, cancellation is handled), so a
            // fault here is not actionable; disposing proceeds regardless.
        }

        _cts?.Dispose();
    }
}
