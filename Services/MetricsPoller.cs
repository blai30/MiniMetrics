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

    public event Action<MetricsSnapshot>? SnapshotReady;

    public MetricsPoller(ISensorSource source, TimeSpan interval)
    {
        _source = source;
        _interval = interval;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            Emit();
        }
        while (await WaitSafelyAsync(timer, token));
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

    private static async Task<bool> WaitSafelyAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
