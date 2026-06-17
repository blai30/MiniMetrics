using System;
using MiniMetrics.Models;

namespace MiniMetrics.Services;

// Builds a MetricsSnapshot from a hardware tree. It owns the gating (a released device emits no
// section), the sensor-name choices, the GPU power fallback, and the unit conversions, all of which
// are unit-testable against a fake IHardwareTree. The LibreHardwareMonitor specifics live behind the
// tree, so this class never touches the library.
public sealed class HardwareSensorSource : ISensorSource, IDisposable
{
    private readonly IHardwareTree _tree;

    private bool _cpuActive = true;
    private bool _memoryActive = true;
    private bool _gpuActive = true;

    public HardwareSensorSource(IHardwareTree tree) => _tree = tree;

    // Releases or restores devices. A released device is both unloaded from the tree and skipped when
    // building the snapshot, so the app stops reading it entirely once all its metrics are hidden.
    public void SetActiveDevices(bool cpu, bool memory, bool gpu)
    {
        _cpuActive = cpu;
        _memoryActive = memory;
        _gpuActive = gpu;
        _tree.SetEnabled(cpu, memory, gpu);
    }

    public MetricsSnapshot Read()
    {
        _tree.Refresh();

        CpuMetrics? cpu = null;
        if (_cpuActive)
        {
            double load = _tree.Read(HardwareKind.Cpu, SensorKind.Load, "CPU Total") ?? 0;
            // CPU temperature and power are deferred to a later plan (both need the kernel driver).
            cpu = new CpuMetrics(load, null, null);
        }

        MemoryMetrics? memory = null;
        if (_memoryActive)
        {
            double usedGib = _tree.Read(HardwareKind.Memory, SensorKind.Data, "Memory Used") ?? 0;
            double availableGib = _tree.Read(HardwareKind.Memory, SensorKind.Data, "Memory Available") ?? 0;
            memory = new MemoryMetrics(
                GibToBytes(usedGib),
                GibToBytes(usedGib + availableGib));
        }

        GpuMetrics? gpu = null;
        if (_gpuActive && _tree.HasGpu)
        {
            double load = _tree.Read(HardwareKind.Gpu, SensorKind.Load, "GPU Core") ?? 0;
            double temp = _tree.Read(HardwareKind.Gpu, SensorKind.Temperature, "GPU Core") ?? 0;
            double power = _tree.Read(HardwareKind.Gpu, SensorKind.Power, "GPU Package")
                           ?? _tree.Read(HardwareKind.Gpu, SensorKind.Power, "GPU Power") ?? 0;
            double vramUsedMib = _tree.Read(HardwareKind.Gpu, SensorKind.SmallData, "GPU Memory Used") ?? 0;
            double vramTotalMib = _tree.Read(HardwareKind.Gpu, SensorKind.SmallData, "GPU Memory Total") ?? 0;

            gpu = new GpuMetrics(
                load,
                temp,
                MibToBytes(vramUsedMib),
                MibToBytes(vramTotalMib),
                power);
        }

        return new MetricsSnapshot(cpu, memory, gpu);
    }

    private static ulong GibToBytes(double gib) => (ulong)(gib * 1024d * 1024d * 1024d);

    private static ulong MibToBytes(double mib) => (ulong)(mib * 1024d * 1024d);

    public void Dispose() => _tree.Dispose();
}
