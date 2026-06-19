using System;

namespace MiniMetrics.Services;

// The physical device groups the widget can read.
public enum HardwareKind
{
    Cpu,
    Memory,
    Gpu
}

// The sensor categories the widget reads, decoupled from LibreHardwareMonitor's own SensorType.
public enum SensorKind
{
    Load,
    Temperature,
    Power,
    Data,
    SmallData
}

// A port over the physical hardware tree. It hides LibreHardwareMonitor entirely so the sensor
// source (gating, unit conversion, sensor-name choices) can be unit-tested against a fake, and so
// the "all metrics hidden releases the device" rule is observable: SetEnabled(false) unloads a
// group and Read then returns null for it.
public interface IHardwareTree : IDisposable
{
    // Loads or unloads each device group. An unloaded group is removed from the tree and stops
    // refreshing, so the process holds no live handle to that device.
    void SetEnabled(bool cpu, bool memory, bool gpu);

    // Refreshes the currently-loaded hardware so the next reads return fresh values.
    void Refresh();

    // Whether an NVIDIA GPU is present in the tree.
    bool HasGpu { get; }

    // The value of the first sensor of the given kind on the device whose name contains the text, or
    // null if the device is unloaded or no such sensor exists.
    double? Read(HardwareKind device, SensorKind sensor, string nameContains);
}
