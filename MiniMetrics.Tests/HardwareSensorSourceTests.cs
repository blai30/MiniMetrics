using MiniMetrics.Services;
using Xunit;

namespace MiniMetrics.Tests;

public class HardwareSensorSourceTests
{
    private const ulong BytesPerGib = 1024UL * 1024 * 1024;
    private const ulong BytesPerMib = 1024UL * 1024;

    [Fact]
    public void Read_maps_cpu_load()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.NotNull(snapshot.Cpu);
        Assert.Equal(34, snapshot.Cpu!.UsagePercent);
    }

    [Fact]
    public void Read_maps_cpu_temperature_and_power()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        tree.Set(HardwareKind.Cpu, SensorKind.Temperature, "CPU Package", 56);
        tree.Set(HardwareKind.Cpu, SensorKind.Power, "CPU Package", 65);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.Equal(56, snapshot.Cpu!.TempCelsius);
        Assert.Equal(65, snapshot.Cpu.PowerWatts);
    }

    [Fact]
    public void Cpu_temp_and_power_fall_back_to_amd_sensor_names()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        tree.Set(HardwareKind.Cpu, SensorKind.Temperature, "Core (Tctl/Tdie)", 61);
        tree.Set(HardwareKind.Cpu, SensorKind.Power, "Package", 88);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.Equal(61, snapshot.Cpu!.TempCelsius);
        Assert.Equal(88, snapshot.Cpu.PowerWatts);
    }

    [Fact]
    public void Cpu_temp_and_power_are_null_when_their_sensors_are_absent()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.Null(snapshot.Cpu!.TempCelsius);
        Assert.Null(snapshot.Cpu.PowerWatts);
    }

    [Fact]
    public void Read_converts_memory_gib_to_bytes()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Memory, SensorKind.Data, "Memory Used", 8);
        tree.Set(HardwareKind.Memory, SensorKind.Data, "Memory Available", 8);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.Equal(8UL * BytesPerGib, snapshot.Memory!.UsedBytes);
        Assert.Equal(16UL * BytesPerGib, snapshot.Memory.TotalBytes);
    }

    [Fact]
    public void Read_maps_gpu_and_converts_vram_mib_to_bytes()
    {
        var tree = new FakeHardwareTree { HasGpu = true };
        tree.Set(HardwareKind.Gpu, SensorKind.Load, "GPU Core", 78);
        tree.Set(HardwareKind.Gpu, SensorKind.Temperature, "GPU Core", 71);
        tree.Set(HardwareKind.Gpu, SensorKind.SmallData, "GPU Memory Used", 6144);
        tree.Set(HardwareKind.Gpu, SensorKind.SmallData, "GPU Memory Total", 12288);
        var source = new HardwareSensorSource(tree);

        var snapshot = source.Read();

        Assert.Equal(78, snapshot.Gpu!.UsagePercent);
        Assert.Equal(71, snapshot.Gpu.TempCelsius);
        Assert.Equal(6144UL * BytesPerMib, snapshot.Gpu.VramUsedBytes);
        Assert.Equal(12288UL * BytesPerMib, snapshot.Gpu.VramTotalBytes);
    }

    [Fact]
    public void Gpu_power_prefers_package_then_falls_back_to_gpu_power()
    {
        var withPackage = new FakeHardwareTree();
        withPackage.Set(HardwareKind.Gpu, SensorKind.Power, "GPU Package", 185);
        withPackage.Set(HardwareKind.Gpu, SensorKind.Power, "GPU Power", 999);
        Assert.Equal(185, new HardwareSensorSource(withPackage).Read().Gpu!.PowerWatts);

        var fallbackOnly = new FakeHardwareTree();
        fallbackOnly.Set(HardwareKind.Gpu, SensorKind.Power, "GPU Power", 150);
        Assert.Equal(150, new HardwareSensorSource(fallbackOnly).Read().Gpu!.PowerWatts);
    }

    [Fact]
    public void Gpu_absent_yields_a_null_gpu_section()
    {
        var tree = new FakeHardwareTree { HasGpu = false };
        tree.Set(HardwareKind.Gpu, SensorKind.Load, "GPU Core", 78);
        var source = new HardwareSensorSource(tree);

        Assert.Null(source.Read().Gpu);
    }

    [Fact]
    public void Releasing_a_device_unloads_it_from_the_tree()
    {
        var tree = new FakeHardwareTree();
        var source = new HardwareSensorSource(tree);

        source.SetActiveDevices(cpu: false, memory: true, gpu: false);

        // The unload call actually reaches the tree, so the process drops its handle to the device.
        Assert.False(tree.CpuEnabled);
        Assert.True(tree.MemoryEnabled);
        Assert.False(tree.GpuEnabled);
    }

    [Fact]
    public void A_released_device_is_not_polled()
    {
        var tree = new FakeHardwareTree();
        tree.Set(HardwareKind.Cpu, SensorKind.Load, "CPU Total", 34);
        var source = new HardwareSensorSource(tree);

        source.SetActiveDevices(cpu: false, memory: true, gpu: true);
        var snapshot = source.Read();

        // Even though a CPU value is present, a released device emits no section.
        Assert.Null(snapshot.Cpu);
        Assert.NotNull(snapshot.Memory);
    }

    [Fact]
    public void Read_refreshes_the_tree_each_call()
    {
        var tree = new FakeHardwareTree();
        var source = new HardwareSensorSource(tree);

        source.Read();
        source.Read();

        Assert.Equal(2, tree.RefreshCount);
    }

    [Fact]
    public void Dispose_disposes_the_tree()
    {
        var tree = new FakeHardwareTree();
        var source = new HardwareSensorSource(tree);

        source.Dispose();

        Assert.True(tree.Disposed);
    }
}
