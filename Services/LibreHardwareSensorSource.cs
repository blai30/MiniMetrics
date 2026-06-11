using System;
using System.Linq;
using DesktopMetrics.Models;
using LibreHardwareMonitor.Hardware;

namespace DesktopMetrics.Services;

public sealed class LibreHardwareSensorSource : ISensorSource, IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();

    public LibreHardwareSensorSource()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsMemoryEnabled = true,
            IsGpuEnabled = true,
        };
        _computer.Open();
    }

    public MetricsSnapshot Read()
    {
        _computer.Accept(_visitor);

        IHardware? cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        // LibreHardwareMonitor exposes two memory nodes: "Virtual Memory" (commit charge, includes the
        // page file) and "Total Memory" (installed physical RAM). We want physical RAM.
        IHardware? memory =
            _computer.Hardware.FirstOrDefault(h =>
                h.HardwareType == HardwareType.Memory &&
                h.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
            ?? _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);

        IHardware? gpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia);

        double cpuLoad = SensorValue(cpu, SensorType.Load, "CPU Total") ?? 0;
        var cpuMetrics = new CpuMetrics(cpuLoad, null); // CPU temperature is deferred to a later plan.

        double usedGib = SensorValue(memory, SensorType.Data, "Memory Used") ?? 0;
        double availableGib = SensorValue(memory, SensorType.Data, "Memory Available") ?? 0;
        var memoryMetrics = new MemoryMetrics(
            GibToBytes(usedGib),
            GibToBytes(usedGib + availableGib));

        GpuMetrics? gpuMetrics = null;
        if (gpu is not null)
        {
            double gpuLoad = SensorValue(gpu, SensorType.Load, "GPU Core") ?? 0;
            double gpuTemp = SensorValue(gpu, SensorType.Temperature, "GPU Core") ?? 0;
            double gpuPower = SensorValue(gpu, SensorType.Power, "GPU Package")
                              ?? SensorValue(gpu, SensorType.Power, "GPU Power") ?? 0;
            double vramUsedMib = SensorValue(gpu, SensorType.SmallData, "GPU Memory Used") ?? 0;
            double vramTotalMib = SensorValue(gpu, SensorType.SmallData, "GPU Memory Total") ?? 0;

            gpuMetrics = new GpuMetrics(
                gpuLoad,
                gpuTemp,
                MibToBytes(vramUsedMib),
                MibToBytes(vramTotalMib),
                gpuPower);
        }

        return new MetricsSnapshot(cpuMetrics, memoryMetrics, gpuMetrics);
    }

    private static double? SensorValue(IHardware? hardware, SensorType type, string nameContains)
    {
        if (hardware is null)
        {
            return null;
        }

        ISensor? sensor = hardware.Sensors.FirstOrDefault(s =>
            s.SensorType == type &&
            s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));

        return sensor?.Value is float value ? value : null;
    }

    private static ulong GibToBytes(double gib) => (ulong)(gib * 1024d * 1024d * 1024d);

    private static ulong MibToBytes(double mib) => (ulong)(mib * 1024d * 1024d);

    public void Dispose() => _computer.Close();
}
