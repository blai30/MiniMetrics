using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.Tests;

[TestClass]
public class RowBuilderTests
{
    private static MetricsSnapshot WithGpu(double gpuTemp = 71.0) => new(
        new(34.0, null, null),
        new(12_026_124_800UL, 34_359_738_368UL),
        new(78.0, gpuTemp, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    [TestMethod]
    public void Build_with_gpu_produces_four_rows_in_order()
    {
        var rows = RowBuilder.Build(WithGpu());
        CollectionAssert.AreEqual(new[] { "cpu", "ram", "gpu", "vram" }, rows.Select(r => r.Key).ToArray());
    }

    [TestMethod]
    public void Build_formats_cpu_and_ram_rows()
    {
        var rows = RowBuilder.Build(WithGpu());
        var cpu = rows.Single(r => r.Key == "cpu");
        var ram = rows.Single(r => r.Key == "ram");

        Assert.AreEqual("34", cpu.Value);
        Assert.AreEqual("—", cpu.Temp); // CPU temp unavailable (null): muted placeholder
        Assert.AreEqual(TempLevel.None, cpu.TempLevel);
        Assert.AreEqual("—", cpu.Detail); // CPU power unavailable (null): muted placeholder
        Assert.AreEqual(RowColor.Cyan, cpu.Color);

        Assert.AreEqual("11.2", ram.Value);
        Assert.AreEqual("", ram.Temp);
        Assert.AreEqual("/ 32 GB", ram.Detail);
        Assert.AreEqual(RowColor.Green, ram.Color);
    }

    [TestMethod]
    public void Build_formats_gpu_and_vram_rows()
    {
        var rows = RowBuilder.Build(WithGpu());
        var gpu = rows.Single(r => r.Key == "gpu");
        var vram = rows.Single(r => r.Key == "vram");

        Assert.AreEqual("78", gpu.Value);
        Assert.AreEqual("71", gpu.Temp);
        Assert.AreEqual("185 W", gpu.Detail);
        Assert.AreEqual(RowColor.Amber, gpu.Color);

        Assert.AreEqual("6.4", vram.Value);
        Assert.AreEqual("/ 12 GB", vram.Detail);
        Assert.AreEqual(RowColor.Violet, vram.Color);
    }

    [TestMethod]
    [DataRow(39.0, TempLevel.Frigid)]
    [DataRow(40.0, TempLevel.Cold)]
    [DataRow(49.0, TempLevel.Cold)]
    [DataRow(50.0, TempLevel.Cool)]
    [DataRow(59.0, TempLevel.Cool)]
    [DataRow(60.0, TempLevel.Warm)]
    [DataRow(69.0, TempLevel.Warm)]
    [DataRow(70.0, TempLevel.Hot)]
    [DataRow(79.0, TempLevel.Hot)]
    [DataRow(80.0, TempLevel.Critical)]
    [DataRow(95.0, TempLevel.Critical)]
    public void Build_color_codes_gpu_temperature_by_threshold(double temp, TempLevel expected)
    {
        var gpu = RowBuilder.Build(WithGpu(temp)).Single(r => r.Key == "gpu");
        Assert.AreEqual(expected, gpu.TempLevel);
    }

    [TestMethod]
    public void Build_formats_cpu_temperature_and_power_when_present()
    {
        var snapshot = new MetricsSnapshot(
            new(34.0, 56.0, 65.0),
            new(12_026_124_800UL, 34_359_738_368UL),
            null);

        var cpu = RowBuilder.Build(snapshot).Single(r => r.Key == "cpu");

        Assert.AreEqual("56", cpu.Temp);
        Assert.AreEqual("65 W", cpu.Detail);
    }

    [TestMethod]
    [DataRow(39.0, TempLevel.Frigid)]
    [DataRow(40.0, TempLevel.Cold)]
    [DataRow(50.0, TempLevel.Cool)]
    [DataRow(59.0, TempLevel.Cool)]
    [DataRow(60.0, TempLevel.Warm)]
    [DataRow(70.0, TempLevel.Hot)]
    [DataRow(80.0, TempLevel.Critical)]
    [DataRow(95.0, TempLevel.Critical)]
    public void Build_color_codes_cpu_temperature_by_threshold(double temp, TempLevel expected)
    {
        var snapshot = new MetricsSnapshot(
            new(34.0, temp, 65.0),
            new(12_026_124_800UL, 34_359_738_368UL),
            null);

        var cpu = RowBuilder.Build(snapshot).Single(r => r.Key == "cpu");

        Assert.AreEqual(expected, cpu.TempLevel);
    }

    [TestMethod]
    public void Build_without_gpu_omits_gpu_and_vram_rows()
    {
        var snapshot = WithGpu() with { Gpu = null };
        var rows = RowBuilder.Build(snapshot);
        CollectionAssert.AreEqual(new[] { "cpu", "ram" }, rows.Select(r => r.Key).ToArray());
    }

    [TestMethod]
    public void Build_without_cpu_section_omits_cpu_row()
    {
        var rows = RowBuilder.Build(WithGpu() with { Cpu = null });
        CollectionAssert.AreEqual(new[] { "ram", "gpu", "vram" }, rows.Select(r => r.Key).ToArray());
    }

    [TestMethod]
    public void Build_without_memory_section_omits_ram_row()
    {
        var rows = RowBuilder.Build(WithGpu() with { Memory = null });
        CollectionAssert.AreEqual(new[] { "cpu", "gpu", "vram" }, rows.Select(r => r.Key).ToArray());
    }
}
