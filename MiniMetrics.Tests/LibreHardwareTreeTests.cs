using LibreHardwareMonitor.Hardware;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class LibreHardwareTreeTests
{
    [TestMethod]
    public void Prefers_discrete_nvidia_over_integrated_intel()
    {
        // A hybrid laptop enumerates the Intel iGPU first; the widget must still report the NVIDIA dGPU.
        var chosen = LibreHardwareTree.PreferredGpuType([HardwareType.GpuIntel, HardwareType.GpuNvidia]);

        Assert.AreEqual(HardwareType.GpuNvidia, chosen);
    }

    [TestMethod]
    public void Prefers_discrete_amd_over_integrated_intel()
    {
        var chosen = LibreHardwareTree.PreferredGpuType([HardwareType.GpuIntel, HardwareType.GpuAmd]);

        Assert.AreEqual(HardwareType.GpuAmd, chosen);
    }

    [TestMethod]
    public void Falls_back_to_intel_when_it_is_the_only_gpu()
    {
        var chosen = LibreHardwareTree.PreferredGpuType([HardwareType.GpuIntel]);

        Assert.AreEqual(HardwareType.GpuIntel, chosen);
    }

    [TestMethod]
    public void Returns_null_when_no_gpu_is_present()
    {
        var chosen = LibreHardwareTree.PreferredGpuType([HardwareType.Cpu, HardwareType.Memory]);

        Assert.IsNull(chosen);
    }

    private const ulong BytesPerGib = 1024UL * 1024 * 1024;

    [TestMethod]
    public void Memory_reading_reports_used_and_available_in_gib()
    {
        double? used = LibreHardwareTree.MemoryReading(SensorKind.Data, "Memory Used", 32 * BytesPerGib, 12 * BytesPerGib);
        double? available = LibreHardwareTree.MemoryReading(SensorKind.Data, "Memory Available", 32 * BytesPerGib, 12 * BytesPerGib);

        Assert.AreEqual(20d, used);
        Assert.AreEqual(12d, available);
    }

    [TestMethod]
    public void Memory_reading_returns_null_for_unknown_sensors()
    {
        Assert.IsNull(LibreHardwareTree.MemoryReading(SensorKind.Temperature, "Memory Used", 32 * BytesPerGib, 12 * BytesPerGib));
        Assert.IsNull(LibreHardwareTree.MemoryReading(SensorKind.Data, "DIMM", 32 * BytesPerGib, 12 * BytesPerGib));
    }
}
