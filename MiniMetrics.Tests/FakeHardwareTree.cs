using System.Collections.Generic;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

// An in-memory hardware tree: tests seed sensor values and observe which device groups were
// enabled/unloaded, with no LibreHardwareMonitor or real hardware involved.
public sealed class FakeHardwareTree : IHardwareTree
{
    private readonly Dictionary<(HardwareKind, SensorKind, string), double> _values = new();

    public bool HasGpu { get; set; } = true;
    public int RefreshCount { get; private set; }
    public bool Disposed { get; private set; }

    public bool CpuEnabled { get; private set; } = true;
    public bool MemoryEnabled { get; private set; } = true;
    public bool GpuEnabled { get; private set; } = true;

    public void Set(HardwareKind device, SensorKind sensor, string nameContains, double value)
        => _values[(device, sensor, nameContains)] = value;

    public void SetEnabled(bool cpu, bool memory, bool gpu)
    {
        CpuEnabled = cpu;
        MemoryEnabled = memory;
        GpuEnabled = gpu;
    }

    public void Refresh() => RefreshCount++;

    public double? Read(HardwareKind device, SensorKind sensor, string nameContains)
        => _values.TryGetValue((device, sensor, nameContains), out double value) ? value : null;

    public void Dispose() => Disposed = true;
}
