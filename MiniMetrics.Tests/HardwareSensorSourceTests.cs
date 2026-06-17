using MiniMetrics.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class HardwareSensorSourceTests
{
    private const ulong BytesPerGib = 1024UL * 1024 * 1024;
    private const ulong BytesPerMib = 1024UL * 1024;

    [TestMethod]
    public void Read_maps_cpu_load()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.IsNotNull(snapshot.Cpu);
        Assert.AreEqual(34, snapshot.Cpu!.UsagePercent);
    }

    [TestMethod]
    public void Read_maps_cpu_temperature_and_power()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        tree.Set(HardwareKind.Cpu, SensorKind.Temperature, "CPU Package", 56);
        tree.Set(HardwareKind.Cpu, SensorKind.Power, "CPU Package", 65);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.AreEqual(56, snapshot.Cpu!.TempCelsius);
        Assert.AreEqual(65, snapshot.Cpu.PowerWatts);
    }

    [TestMethod]
    public void Cpu_temp_and_power_fall_back_to_amd_sensor_names()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        tree.Set(HardwareKind.Cpu, SensorKind.Temperature, "Core (Tctl/Tdie)", 61);
        tree.Set(HardwareKind.Cpu, SensorKind.Power, "Package", 88);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.AreEqual(61, snapshot.Cpu!.TempCelsius);
        Assert.AreEqual(88, snapshot.Cpu.PowerWatts);
    }

    [TestMethod]
    public void Cpu_temp_and_power_are_null_when_their_sensors_are_absent()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.IsNull(snapshot.Cpu!.TempCelsius);
        Assert.IsNull(snapshot.Cpu.PowerWatts);
    }

    [TestMethod]
    public void Cpu_temp_and_power_are_null_when_their_sensors_read_zero()
    {
        // The PawnIO driver reports 0 for these sensors when it cannot supply a value (not installed,
        // or the process is not elevated to open its device). A running CPU is never at 0 C or 0 W, so
        // the widget should show a placeholder rather than a misleading reading.
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 20);
        tree.Set(HardwareKind.Cpu, SensorKind.Temperature, "Core (Tctl/Tdie)", 0);
        tree.Set(HardwareKind.Cpu, SensorKind.Power, "Package", 0);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.IsNull(snapshot.Cpu!.TempCelsius);
        Assert.IsNull(snapshot.Cpu.PowerWatts);
    }

    [TestMethod]
    public void Read_converts_memory_gib_to_bytes()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Memory, SensorKind.Data, "Memory Used", 8);
        tree.Set(HardwareKind.Memory, SensorKind.Data, "Memory Available", 8);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.AreEqual(8UL * BytesPerGib, snapshot.Memory!.UsedBytes);
        Assert.AreEqual(16UL * BytesPerGib, snapshot.Memory.TotalBytes);
    }

    [TestMethod]
    public void Read_maps_gpu_and_converts_vram_mib_to_bytes()
    {
        var tree = new FakeHardwareTree { HasGpu = true };
        tree.Set(HardwareKind.Gpu, SensorKind.Load, "GPU Core", 78);
        tree.Set(HardwareKind.Gpu, SensorKind.Temperature, "GPU Core", 71);
        tree.Set(HardwareKind.Gpu, SensorKind.SmallData, "GPU Memory Used", 6144);
        tree.Set(HardwareKind.Gpu, SensorKind.SmallData, "GPU Memory Total", 12288);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.AreEqual(78, snapshot.Gpu!.UsagePercent);
        Assert.AreEqual(71, snapshot.Gpu.TempCelsius);
        Assert.AreEqual(6144UL * BytesPerMib, snapshot.Gpu.VramUsedBytes);
        Assert.AreEqual(12288UL * BytesPerMib, snapshot.Gpu.VramTotalBytes);
    }

    [TestMethod]
    public void Gpu_power_prefers_package_then_falls_back_to_gpu_power()
    {
        var withPackage = new FakeHardwareTree();
        withPackage.Set(HardwareKind.Gpu, SensorKind.Power, "GPU Package", 185);
        withPackage.Set(HardwareKind.Gpu, SensorKind.Power, "GPU Power", 999);
        Assert.AreEqual(185, new HardwareSensorSource(withPackage).Read().Gpu!.PowerWatts);

        var fallbackOnly = new FakeHardwareTree();
        fallbackOnly.Set(HardwareKind.Gpu, SensorKind.Power, "GPU Power", 150);
        Assert.AreEqual(150, new HardwareSensorSource(fallbackOnly).Read().Gpu!.PowerWatts);
    }

    [TestMethod]
    public void Gpu_absent_yields_a_null_gpu_section()
    {
        var tree = new FakeHardwareTree { HasGpu = false };
        tree.Set(HardwareKind.Gpu, SensorKind.Load, "GPU Core", 78);
        var source = new HardwareSensorSource(tree);

        Assert.IsNull(source.Read().Gpu);
    }

    [TestMethod]
    public void Releasing_a_device_unloads_it_from_the_tree()
    {
        var tree = new FakeHardwareTree();
        var source = new HardwareSensorSource(tree);

        source.SetActiveDevices(cpu: false, memory: true, gpu: false);

        // The unload call actually reaches the tree, so the process drops its handle to the device.
        Assert.IsFalse(tree.CpuEnabled);
        Assert.IsTrue(tree.MemoryEnabled);
        Assert.IsFalse(tree.GpuEnabled);
    }

    [TestMethod]
    public void A_released_device_is_not_polled()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        var source = new HardwareSensorSource(tree);

        source.SetActiveDevices(cpu: false, memory: true, gpu: true);
        var snapshot = source.Read();

        // Even though a CPU value is present, a released device emits no section.
        Assert.IsNull(snapshot.Cpu);
        Assert.IsNotNull(snapshot.Memory);
    }

    [TestMethod]
    public void Read_refreshes_the_tree_each_call()
    {
        var tree = new FakeHardwareTree();
        var source = new HardwareSensorSource(tree);

        source.Read();
        source.Read();

        Assert.AreEqual(2, tree.RefreshCount);
    }

    [TestMethod]
    public void Dispose_disposes_the_tree()
    {
        var tree = new FakeHardwareTree();
        var source = new HardwareSensorSource(tree);

        source.Dispose();

        Assert.IsTrue(tree.Disposed);
    }
}
