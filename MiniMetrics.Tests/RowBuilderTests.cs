using System.Linq;
using MiniMetrics.Lib;
using MiniMetrics.Models;
using Xunit;

namespace MiniMetrics.Tests;

public class RowBuilderTests
{
    private static MetricsSnapshot WithGpu(double gpuTemp = 71.0) => new(
        new CpuMetrics(34.0, null, null),
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

        Assert.Equal("34", cpu.Value);
        Assert.Equal("—", cpu.Temp); // CPU temp deferred: muted placeholder
        Assert.Equal(TempLevel.None, cpu.TempLevel);
        Assert.Equal("—", cpu.Detail); // CPU power deferred: muted placeholder
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

        Assert.Equal("78", gpu.Value);
        Assert.Equal("71", gpu.Temp);
        Assert.Equal("185 W", gpu.Detail);
        Assert.Equal(RowColor.Amber, gpu.Color);

        Assert.Equal("6.4", vram.Value);
        Assert.Equal("/ 12 GB", vram.Detail);
        Assert.Equal(RowColor.Violet, vram.Color);
    }

    [Theory]
    [InlineData(55.0, TempLevel.Cool)]
    [InlineData(59.0, TempLevel.Cool)]
    [InlineData(60.0, TempLevel.Warm)]
    [InlineData(69.0, TempLevel.Warm)]
    [InlineData(70.0, TempLevel.Hot)]
    [InlineData(79.0, TempLevel.Hot)]
    [InlineData(80.0, TempLevel.Critical)]
    [InlineData(95.0, TempLevel.Critical)]
    public void Build_color_codes_gpu_temperature_by_threshold(double temp, TempLevel expected)
    {
        var gpu = RowBuilder.Build(WithGpu(temp)).Single(r => r.Key == "gpu");
        Assert.Equal(expected, gpu.TempLevel);
    }

    [Fact]
    public void Build_formats_cpu_temperature_and_power_when_present()
    {
        var snapshot = new MetricsSnapshot(
            new CpuMetrics(34.0, 56.0, 65.0),
            new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
            null);

        var cpu = RowBuilder.Build(snapshot).Single(r => r.Key == "cpu");

        Assert.Equal("56", cpu.Temp);
        Assert.Equal("65 W", cpu.Detail);
    }

    [Theory]
    [InlineData(50.0, TempLevel.Cool)]
    [InlineData(64.0, TempLevel.Cool)]
    [InlineData(65.0, TempLevel.Warm)]
    [InlineData(79.0, TempLevel.Warm)]
    [InlineData(80.0, TempLevel.Hot)]
    [InlineData(89.0, TempLevel.Hot)]
    [InlineData(90.0, TempLevel.Critical)]
    public void Build_color_codes_cpu_temperature_by_cpu_threshold(double temp, TempLevel expected)
    {
        var snapshot = new MetricsSnapshot(
            new CpuMetrics(34.0, temp, 65.0),
            new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
            null);

        var cpu = RowBuilder.Build(snapshot).Single(r => r.Key == "cpu");

        Assert.Equal(expected, cpu.TempLevel);
    }

    [Fact]
    public void Build_without_gpu_omits_gpu_and_vram_rows()
    {
        var snapshot = WithGpu() with { Gpu = null };
        var rows = RowBuilder.Build(snapshot);
        Assert.Equal(new[] { "cpu", "ram" }, rows.Select(r => r.Key).ToArray());
    }

    [Fact]
    public void Build_without_cpu_section_omits_cpu_row()
    {
        var rows = RowBuilder.Build(WithGpu() with { Cpu = null });
        Assert.Equal(new[] { "ram", "gpu", "vram" }, rows.Select(r => r.Key).ToArray());
    }

    [Fact]
    public void Build_without_memory_section_omits_ram_row()
    {
        var rows = RowBuilder.Build(WithGpu() with { Memory = null });
        Assert.Equal(new[] { "cpu", "gpu", "vram" }, rows.Select(r => r.Key).ToArray());
    }
}
