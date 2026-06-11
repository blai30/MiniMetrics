using System.Linq;
using DesktopMetrics.Lib;
using DesktopMetrics.Models;
using Xunit;

namespace DesktopMetrics.Tests;

public class RowBuilderTests
{
    private static MetricsSnapshot WithGpu(double gpuTemp = 71.0) => new(
        new CpuMetrics(34.0, null),
        new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
        new GpuMetrics(78.0, gpuTemp, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    [Fact]
    public void Build_with_gpu_produces_four_rows_in_order()
    {
        var rows = RowBuilder.Build(WithGpu());
        Assert.Equal(new[] { "cpu", "ram", "gpu", "vram" }, rows.Select(r => r.Key).ToArray());
    }

    [Fact]
    public void Build_formats_cpu_and_ram_rows()
    {
        var rows = RowBuilder.Build(WithGpu());
        var cpu = rows.Single(r => r.Key == "cpu");
        var ram = rows.Single(r => r.Key == "ram");

        Assert.Equal("34%", cpu.Value);
        Assert.Equal("", cpu.Temp); // no CPU temp in v1
        Assert.Equal(TempLevel.None, cpu.TempLevel);
        Assert.Equal("", cpu.Detail);
        Assert.Equal(RowColor.Cyan, cpu.Color);

        Assert.Equal("11.2", ram.Value);
        Assert.Equal("", ram.Temp);
        Assert.Equal("/ 32 GB", ram.Detail);
        Assert.Equal(RowColor.Green, ram.Color);
    }

    [Fact]
    public void Build_formats_gpu_and_vram_rows()
    {
        var rows = RowBuilder.Build(WithGpu());
        var gpu = rows.Single(r => r.Key == "gpu");
        var vram = rows.Single(r => r.Key == "vram");

        Assert.Equal("78%", gpu.Value);
        Assert.Equal("71°C", gpu.Temp);
        Assert.Equal("· 185W", gpu.Detail);
        Assert.Equal(RowColor.Amber, gpu.Color);

        Assert.Equal("6.4", vram.Value);
        Assert.Equal("/ 12 GB", vram.Detail);
        Assert.Equal(RowColor.Violet, vram.Color);
    }

    [Theory]
    [InlineData(55.0, TempLevel.Cool)]
    [InlineData(64.0, TempLevel.Cool)]
    [InlineData(65.0, TempLevel.Warm)]
    [InlineData(83.0, TempLevel.Warm)]
    [InlineData(84.0, TempLevel.Hot)]
    [InlineData(95.0, TempLevel.Hot)]
    public void Build_color_codes_gpu_temperature_by_threshold(double temp, TempLevel expected)
    {
        var gpu = RowBuilder.Build(WithGpu(temp)).Single(r => r.Key == "gpu");
        Assert.Equal(expected, gpu.TempLevel);
    }

    [Fact]
    public void Build_without_gpu_omits_gpu_and_vram_rows()
    {
        var snapshot = WithGpu() with { Gpu = null };
        var rows = RowBuilder.Build(snapshot);
        Assert.Equal(new[] { "cpu", "ram" }, rows.Select(r => r.Key).ToArray());
    }
}
