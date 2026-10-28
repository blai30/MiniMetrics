using System;
using System.Threading;
using System.Threading.Tasks;
using MiniMetrics.Models;

namespace MiniMetrics.Services;

public sealed class MetricsPoller(ISensorSource source, TimeSpan interval) : IDisposable
{
    private readonly PeriodicTimer _timer = new(interval);
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event Action<MetricsSnapshot>? SnapshotReady;

    public void Start()
    {
        _cts = new();

        // Start the loop is invoked from the UI thread, which carries Avalonia's SynchronizationContext.
        // Task.Run hops to the thread pool so the loop (and every sensor read) runs off the UI thread;
        // otherwise the await continuation would post each read back to the UI thread and stutter drags.
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    // Stops polling while nobody can see the widgets (session locked, display off). The loop stays parked
    // on the timer, so pausing costs nothing.
    public void Pause() => _timer.Period = Timeout.InfiniteTimeSpan;

    // Resumes the regular cadence and reads once right away so the widgets are current when they reappear.
    // Read is serialized by the source, so this extra read cannot overlap the loop's.
    public void Resume()
    {
        _timer.Period = interval;
        Task.Run(Emit);
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            do
            {
                Emit();
            } while (await _timer.WaitForNextTickAsync(token));
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
            var snapshot = source.Read();
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

        _timer.Dispose();
        _cts?.Dispose();
    }
}
