using MiniMetrics.Lib;

namespace MiniMetrics.Tests;

[TestClass]
public class DeviceActivationTests
{
    private static Dictionary<string, bool> AllVisible() => [];

    [TestMethod]
    public void All_visible_and_both_widgets_shown_polls_everything()
    {
        var result = DeviceActivation.Compute(AllVisible(), true, true);
        Assert.IsTrue(result.Cpu);
        Assert.IsTrue(result.Memory);
        Assert.IsTrue(result.Gpu);
    }

    [TestMethod]
    public void Hiding_cpu_widget_stops_cpu_and_ram_only()
    {
        var result = DeviceActivation.Compute(AllVisible(), false, true);
        Assert.IsFalse(result.Cpu);
        Assert.IsFalse(result.Memory);
        Assert.IsTrue(result.Gpu);
    }

    [TestMethod]
    public void Hiding_gpu_widget_stops_gpu_only()
    {
        var result = DeviceActivation.Compute(AllVisible(), true, false);
        Assert.IsTrue(result.Cpu);
        Assert.IsTrue(result.Memory);
        Assert.IsFalse(result.Gpu);
    }

    [TestMethod]
    public void Cpu_widget_shown_but_all_cpu_metrics_hidden_stops_cpu_device()
    {
        var visibility = new Dictionary<string, bool>
        {
            ["cpu.usage"] = false,
            ["cpu.temp"] = false,
            ["cpu.power"] = false
        };
        var result = DeviceActivation.Compute(visibility, true, true);
        Assert.IsFalse(result.Cpu);
        Assert.IsTrue(result.Memory);
    }

    [TestMethod]
    public void Cpu_widget_shown_with_ram_hidden_stops_memory_device()
    {
        var visibility = new Dictionary<string, bool> { ["ram.usage"] = false };
        var result = DeviceActivation.Compute(visibility, true, true);
        Assert.IsFalse(result.Memory);
        Assert.IsTrue(result.Cpu);
    }

    [TestMethod]
    public void Gpu_device_stays_active_for_vram_alone()
    {
        var visibility = new Dictionary<string, bool>
        {
            ["gpu.usage"] = false,
            ["gpu.temp"] = false,
            ["gpu.power"] = false
        };
        var result = DeviceActivation.Compute(visibility, true, true);
        Assert.IsTrue(result.Gpu);
    }

    [TestMethod]
    public void Gpu_widget_shown_but_all_gpu_and_vram_metrics_hidden_stops_gpu_device()
    {
        var visibility = new Dictionary<string, bool>
        {
            ["gpu.usage"] = false,
            ["gpu.temp"] = false,
            ["gpu.power"] = false,
            ["vram.usage"] = false
        };
        var result = DeviceActivation.Compute(visibility, true, true);
        Assert.IsFalse(result.Gpu);
    }
}
