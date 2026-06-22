using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace MiniMetrics.Services;

// The LibreHardwareMonitor adapter behind IHardwareTree. It owns every library-specific quirk: the
// HardwareType/SensorType mapping, picking the physical "Total Memory" node over "Virtual Memory",
// and matching sensors by name. Verified manually on Windows against real hardware; the testable
// logic lives in HardwareSensorSource.
public sealed class LibreHardwareTree : IHardwareTree
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();

    public LibreHardwareTree()
    {
        _computer = new()
        {
            IsCpuEnabled = true,
            IsMemoryEnabled = true,
            IsGpuEnabled = true
        };
        _computer.Open();
    }

    // Toggling IsXxxEnabled adds or removes the hardware from the tree, so a released device stops
    // being refreshed by Accept() and its sensors are no longer read.
    public void SetEnabled(bool cpu, bool memory, bool gpu)
    {
        _computer.IsCpuEnabled = cpu;
        _computer.IsMemoryEnabled = memory;
        _computer.IsGpuEnabled = gpu;
    }

    public void Refresh() => _computer.Accept(_visitor);

    public bool HasGpu => PreferredGpuType(_computer.Hardware.Select(h => h.HardwareType)) is not null;

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
            if (available.Contains(type))
                return type;

        return null;
    }

    public double? Read(HardwareKind device, SensorKind sensor, string nameContains)
    {
        var hardware = Find(device);
        if (hardware is null) return null;

        var type = Map(sensor);
        var match = hardware.Sensors.FirstOrDefault(s =>
            s.SensorType == type &&
            s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));

        return match?.Value;
    }

    private IHardware? Find(HardwareKind device) => device switch
    {
        HardwareKind.Cpu => _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu),

        // LibreHardwareMonitor exposes two memory nodes: "Virtual Memory" (commit charge, includes the
        // page file) and "Total Memory" (installed physical RAM). We want physical RAM.
        HardwareKind.Memory =>
            _computer.Hardware.FirstOrDefault(h =>
                h.HardwareType == HardwareType.Memory &&
                h.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
            ?? _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory),

        HardwareKind.Gpu => PreferredGpuType(_computer.Hardware.Select(h => h.HardwareType)) is { } gpuType
            ? _computer.Hardware.FirstOrDefault(h => h.HardwareType == gpuType)
            : null,

        _ => null
    };

    private static SensorType Map(SensorKind sensor) => sensor switch
    {
        SensorKind.Load => SensorType.Load,
        SensorKind.Temperature => SensorType.Temperature,
        SensorKind.Power => SensorType.Power,
        SensorKind.Data => SensorType.Data,
        SensorKind.SmallData => SensorType.SmallData,
        _ => SensorType.Load
    };

    public void Dispose() => _computer.Close();
}
