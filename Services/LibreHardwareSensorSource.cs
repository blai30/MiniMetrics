using System;
using System.Linq;
using MiniMetrics.Models;
using LibreHardwareMonitor.Hardware;

namespace MiniMetrics.Services;

public sealed class LibreHardwareSensorSource : ISensorSource, IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();

    private bool _cpuActive = true;
    private bool _memoryActive = true;
    private bool _gpuActive = true;

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

    // Toggling IsXxxEnabled adds or removes the hardware from the tree, so a released device stops
    // being refreshed by Accept() and its sensors are no longer read.
    public void SetActiveDevices(bool cpu, bool memory, bool gpu)
    {
        _cpuActive = cpu;
        _memoryActive = memory;
        _gpuActive = gpu;
        _computer.IsCpuEnabled = cpu;
        _computer.IsMemoryEnabled = memory;
        _computer.IsGpuEnabled = gpu;
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

        CpuMetrics? cpuMetrics = null;
        if (_cpuActive)
        {
            double cpuLoad = SensorValue(cpu, SensorType.Load, "CPU Total") ?? 0;
            // CPU temperature and power are deferred to a later plan (both need the kernel driver).
            cpuMetrics = new CpuMetrics(cpuLoad, null, null);
        }

        MemoryMetrics? memoryMetrics = null;
        if (_memoryActive)
        {
            double usedGib = SensorValue(memory, SensorType.Data, "Memory Used") ?? 0;
            double availableGib = SensorValue(memory, SensorType.Data, "Memory Available") ?? 0;
            memoryMetrics = new MemoryMetrics(
                GibToBytes(usedGib),
                GibToBytes(usedGib + availableGib));
        }

        GpuMetrics? gpuMetrics = null;
        if (_gpuActive && gpu is not null)
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
