using System;
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

    public bool HasGpu =>
        _computer.Hardware.Any(hardware => hardware.HardwareType == HardwareType.GpuNvidia);

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

        HardwareKind.Gpu => _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia),

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
