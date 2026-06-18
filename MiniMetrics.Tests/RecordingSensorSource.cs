using MiniMetrics.Models;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

// Records the latest active-device selection so tests can assert which devices a coordinator releases.
public sealed class RecordingSensorSource : ISensorSource
{
    public bool Cpu { get; private set; } = true;
    public bool Memory { get; private set; } = true;
    public bool Gpu { get; private set; } = true;
    public int SetActiveDevicesCount { get; private set; }

    public void SetActiveDevices(bool cpu, bool memory, bool gpu)
    {
        Cpu = cpu;
        Memory = memory;
        Gpu = gpu;
        SetActiveDevicesCount++;
    }

    public MetricsSnapshot Read() => new(null, null, null);
}
