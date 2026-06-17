using System.Collections.Generic;
using MiniMetrics.Lib;
using Xunit;

namespace MiniMetrics.Tests;

public class DeviceActivationTests
{
    private static Dictionary<string, bool> AllVisible() => new();

    [Fact]
    public void All_visible_and_both_widgets_shown_polls_everything()
    {
        var result = DeviceActivation.Compute(AllVisible(), cpuWidgetShown: true, gpuWidgetShown: true);
        Assert.True(result.Cpu);
        Assert.True(result.Memory);
        Assert.True(result.Gpu);
    }

    [Fact]
    public void Hiding_cpu_widget_stops_cpu_and_ram_only()
    {
        var result = DeviceActivation.Compute(AllVisible(), cpuWidgetShown: false, gpuWidgetShown: true);
        Assert.False(result.Cpu);
        Assert.False(result.Memory);
        Assert.True(result.Gpu);
    }

    [Fact]
    public void Hiding_gpu_widget_stops_gpu_only()
    {
        var result = DeviceActivation.Compute(AllVisible(), cpuWidgetShown: true, gpuWidgetShown: false);
        Assert.True(result.Cpu);
        Assert.True(result.Memory);
        Assert.False(result.Gpu);
    }

    [Fact]
    public void Cpu_widget_shown_but_all_cpu_metrics_hidden_stops_cpu_device()
    {
        var visibility = new Dictionary<string, bool>
        {
            ["cpu.usage"] = false,
            ["cpu.temp"] = false,
            ["cpu.power"] = false,
        };
        var result = DeviceActivation.Compute(visibility, cpuWidgetShown: true, gpuWidgetShown: true);
        Assert.False(result.Cpu);
        Assert.True(result.Memory);
    }

    [Fact]
    public void Cpu_widget_shown_with_ram_hidden_stops_memory_device()
    {
        var visibility = new Dictionary<string, bool> { ["ram.usage"] = false };
        var result = DeviceActivation.Compute(visibility, cpuWidgetShown: true, gpuWidgetShown: true);
        Assert.False(result.Memory);
        Assert.True(result.Cpu);
    }

    [Fact]
    public void Gpu_device_stays_active_for_vram_alone()
    {
        var visibility = new Dictionary<string, bool>
        {
            ["gpu.usage"] = false,
            ["gpu.temp"] = false,
            ["gpu.power"] = false,
        };
        var result = DeviceActivation.Compute(visibility, cpuWidgetShown: true, gpuWidgetShown: true);
        Assert.True(result.Gpu);
    }

    [Fact]
    public void Gpu_widget_shown_but_all_gpu_and_vram_metrics_hidden_stops_gpu_device()
    {
        var visibility = new Dictionary<string, bool>
        {
            ["gpu.usage"] = false,
            ["gpu.temp"] = false,
            ["gpu.power"] = false,
            ["vram.usage"] = false,
        };
        var result = DeviceActivation.Compute(visibility, cpuWidgetShown: true, gpuWidgetShown: true);
        Assert.False(result.Gpu);
    }
}
