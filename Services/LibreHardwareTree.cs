using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace MiniMetrics.Services;

// The LibreHardwareMonitor adapter behind IHardwareTree. It owns every library-specific quirk: the
// HardwareType/SensorType mapping, picking the preferred GPU, and matching sensors by name. Verified
// manually on Windows against real hardware; the testable logic lives in HardwareSensorSource.
public sealed class LibreHardwareTree : IHardwareTree
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private bool _memoryEnabled = true;

    // The CPU and preferred GPU are resolved once and reused until SetEnabled rebuilds the device
    // groups (the only way LibreHardwareMonitor replaces CPU or GPU hardware objects).
    private bool _resolved;
    private IHardware? _cpu;
    private IHardware? _gpu;

    // Each device's active sensors, re-taken after every update so a sensor activated lazily during the
    // update is readable on the same tick. Hardware.Sensors copies its list, so it is read once per tick.
    private ISensor[] _cpuSensors = [];
    private ISensor[] _gpuSensors = [];
    private (ulong TotalBytes, ulong AvailableBytes)? _memory;

    public LibreHardwareTree()
    {
        // The memory group stays off: besides RAM usage it detects every DIMM and polls its SPD
        // thermal sensor over SMBus through PawnIO each refresh, a bus shared with RGB controllers
        // and other hardware tools. RAM usage is read directly from Windows instead (Refresh).
        _computer = new()
        {
            IsCpuEnabled = true,
            IsMemoryEnabled = false,
            IsGpuEnabled = true,
        };
        _computer.Open();
    }

    // Toggling IsXxxEnabled adds or removes the hardware from the tree, so a released device stops
    // being refreshed and its sensors are no longer read.
    public void SetEnabled(bool cpu, bool memory, bool gpu)
    {
        // LibreHardwareMonitor only builds its Intel GPU group while the CPU group is loaded, so load the
        // CPU group for the moment the GPU groups are rebuilt, then apply the real CPU flag.
        if (gpu && !_computer.IsGpuEnabled) _computer.IsCpuEnabled = true;

        _computer.IsGpuEnabled = gpu;
        _computer.IsCpuEnabled = cpu;
        _memoryEnabled = memory;
        _resolved = false;
    }

    // Updates only the devices the widget reads. Computer.Accept would also update every other GPU
    // (an integrated GPU next to the discrete one), which costs several milliseconds per tick for
    // values that are never shown.
    public void Refresh()
    {
        Resolve();

        _cpu?.Accept(_visitor);
        _gpu?.Accept(_visitor);
        _cpuSensors = _cpu?.Sensors ?? [];
        _gpuSensors = _gpu?.Sensors ?? [];
        _memory = _memoryEnabled ? PhysicalMemory.Usage() : null;
    }

    public bool HasGpu
    {
        get
        {
            Resolve();
            return _gpu is not null;
        }
    }

    private void Resolve()
    {
        if (_resolved) return;

        var hardware = _computer.Hardware;
        _cpu = hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        _gpu = PreferredGpuType(hardware.Select(h => h.HardwareType)) is { } gpuType
            ? hardware.FirstOrDefault(h => h.HardwareType == gpuType)
            : null;
        _cpuSensors = [];
        _gpuSensors = [];
        _resolved = true;
    }

    // Vendor preference order. A hybrid system (Intel iGPU + NVIDIA/AMD dGPU) enumerates the integrated
    // GPU first, so picking the first match would report the iGPU and miss the discrete card. Prefer the
    // verified NVIDIA path, then AMD, and fall back to the Intel integrated GPU only when it is the sole
    // option. AMD and Intel reads use the sensor-name fallbacks in HardwareSensorSource.
    private static readonly HardwareType[] GpuPriority =
        [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel];

    public static HardwareType? PreferredGpuType(IEnumerable<HardwareType> present)
    {
        var available = present.ToHashSet();
        foreach (var type in GpuPriority)
        {
            if (available.Contains(type))
            {
                return type;
            }
        }

        return null;
    }

    public double? Read(HardwareKind device, SensorKind sensor, string nameContains) => device switch
    {
        HardwareKind.Memory => _memory is var (total, available)
            ? MemoryReading(sensor, nameContains, total, available)
            : null,
        HardwareKind.Cpu => Find(_cpuSensors, Map(sensor), nameContains),
        HardwareKind.Gpu => Find(_gpuSensors, Map(sensor), nameContains),
        _ => null,
    };

    private static double? Find(ISensor[] sensors, SensorType type, string nameContains)
    {
        foreach (var sensor in sensors)
        {
            if (sensor.SensorType == type && sensor.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            {
                return sensor.Value;
            }
        }

        return null;
    }

    // Mirrors the "Memory Used" / "Memory Available" GiB sensors of LibreHardwareMonitor's "Total
    // Memory" node, so HardwareSensorSource reads RAM the same way it always has.
    public static double? MemoryReading(SensorKind sensor, string nameContains, ulong totalBytes, ulong availableBytes)
    {
        if (sensor != SensorKind.Data) return null;

        const double bytesPerGib = 1024d * 1024d * 1024d;
        if ("Memory Used".Contains(nameContains, StringComparison.OrdinalIgnoreCase))
        {
            return (totalBytes - availableBytes) / bytesPerGib;
        }

        return "Memory Available".Contains(nameContains, StringComparison.OrdinalIgnoreCase)
            ? availableBytes / bytesPerGib
            : null;
    }

    private static SensorType Map(SensorKind sensor) => sensor switch
    {
        SensorKind.Load => SensorType.Load,
        SensorKind.Temperature => SensorType.Temperature,
        SensorKind.Power => SensorType.Power,
        SensorKind.Data => SensorType.Data,
        SensorKind.SmallData => SensorType.SmallData,
        _ => SensorType.Load,
    };

    public void Dispose() => _computer.Close();
}
