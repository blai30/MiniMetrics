using System;
using System.Threading;
using MiniMetrics.Models;

namespace MiniMetrics.Services;

// Builds a MetricsSnapshot from a hardware tree. It owns the gating (a released device emits no
// section), the sensor-name choices, the GPU power fallback, and the unit conversions, all of which
// are unit-testable against a fake IHardwareTree. The LibreHardwareMonitor specifics live behind the
// tree, so this class never touches the library.
public sealed class HardwareSensorSource(IHardwareTree tree, Func<ulong>? installedMemoryBytes = null)
    : ISensorSource, IDisposable
{
    private readonly ulong _installedMemoryBytes = (installedMemoryBytes ?? PhysicalMemory.InstalledBytes)();

    // Serializes every touch of the underlying tree. SetActiveDevices runs on the UI thread while Read
    // runs on the poll thread, and both enumerate/mutate the same LibreHardwareMonitor hardware
    // collection; concurrent access there can fault the PawnIO driver. The lock keeps tree access
    // single-threaded without moving the read off the poll thread.
    private readonly Lock _treeLock = new();

    private bool _cpuActive = true;
    private bool _memoryActive = true;
    private bool _gpuActive = true;

    // installedMemoryBytes defaults to the firmware-reported installed RAM; tests inject a fixed value.
    // It is read once because installed memory does not change while the process runs.

    // Releases or restores devices. A released device is both unloaded from the tree and skipped when
    // building the snapshot, so the app stops reading it entirely once all its metrics are hidden.
    public void SetActiveDevices(bool cpu, bool memory, bool gpu)
    {
        lock (_treeLock)
        {
            _cpuActive = cpu;
            _memoryActive = memory;
            _gpuActive = gpu;
            tree.SetEnabled(cpu, memory, gpu);
        }
    }

    public MetricsSnapshot Read()
    {
        lock (_treeLock)
        {
            return ReadLocked();
        }
    }

    private MetricsSnapshot ReadLocked()
    {
        tree.Refresh();

        CpuMetrics? cpu = null;
        if (_cpuActive)
        {
            double load = tree.Read(HardwareKind.Cpu, SensorKind.Load, "CPU Total") ?? 0;
            // CPU package temperature and power are read through the PawnIO kernel driver. When that
            // driver cannot supply a value (it is not installed, or the process is not elevated to open
            // its device) the sensors still exist but report 0. A running CPU is never at 0 C or 0 W, so
            // Available maps a non-positive (or absent) reading to null and the widget shows a
            // placeholder. Intel exposes "CPU Package"; AMD surfaces "Core (Tctl/Tdie)" for temperature
            // and "Package" for power.
            double? temp = Available(tree.Read(HardwareKind.Cpu, SensorKind.Temperature, "CPU Package")) ??
                           Available(tree.Read(HardwareKind.Cpu, SensorKind.Temperature, "Core (Tctl/Tdie)"));
            double? power = Available(tree.Read(HardwareKind.Cpu, SensorKind.Power, "CPU Package")) ??
                            Available(tree.Read(HardwareKind.Cpu, SensorKind.Power, "Package"));
            cpu = new(load, temp, power);
        }

        MemoryMetrics? memory = null;
        if (_memoryActive)
        {
            double usedGib = tree.Read(HardwareKind.Memory, SensorKind.Data, "Memory Used") ?? 0;
            double availableGib = tree.Read(HardwareKind.Memory, SensorKind.Data, "Memory Available") ?? 0;
            ulong usedBytes = GibToBytes(usedGib);
            ulong usableTotalBytes = GibToBytes(usedGib + availableGib);

            // The firmware-reported installed total includes hardware-reserved memory the OS cannot
            // address. Counting that reserve as used reports the full installed size while keeping
            // used + available equal to the total. When the installed figure is unavailable or not
            // larger than the usable total, fall back to the usable total so the reserve never
            // underflows.
            memory = _installedMemoryBytes > usableTotalBytes
                ? new(usedBytes + (_installedMemoryBytes - usableTotalBytes), _installedMemoryBytes)
                : new MemoryMetrics(usedBytes, usableTotalBytes);
        }

        GpuMetrics? gpu = null;
        if (_gpuActive && tree.HasGpu)
        {
            double load = tree.Read(HardwareKind.Gpu, SensorKind.Load, "GPU Core") ?? 0;
            double temp = tree.Read(HardwareKind.Gpu, SensorKind.Temperature, "GPU Core") ?? 0;
            double power = tree.Read(HardwareKind.Gpu, SensorKind.Power, "GPU Package")
                           ?? tree.Read(HardwareKind.Gpu, SensorKind.Power, "GPU Power") ?? 0;
            double vramUsedMib = tree.Read(HardwareKind.Gpu, SensorKind.SmallData, "GPU Memory Used") ?? 0;
            double vramTotalMib = tree.Read(HardwareKind.Gpu, SensorKind.SmallData, "GPU Memory Total") ?? 0;

            gpu = new(
                load,
                temp,
                MibToBytes(vramUsedMib),
                MibToBytes(vramTotalMib),
                power);
        }

        return new(cpu, memory, gpu);
    }

    // A CPU package temperature or power reading is only real when positive; 0 or null means the
    // PawnIO driver could not provide it, which the caller treats as unavailable.
    private static double? Available(double? reading) => reading is > 0 ? reading : null;

    private static ulong GibToBytes(double gib) => (ulong)(gib * 1024d * 1024d * 1024d);

    private static ulong MibToBytes(double mib) => (ulong)(mib * 1024d * 1024d);

    public void Dispose() => tree.Dispose();
}
