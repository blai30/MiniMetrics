using MiniMetrics.Models;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Tests;

[TestClass]
public class MetricWidgetViewModelTests
{
    private static MetricsSnapshot WithGpu() => new(
        new CpuMetrics(34.0, null, null),
        new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
        new GpuMetrics(78.0, 71.0, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    private static MetricWidgetViewModel Cpu() => new("cpu", "ram");
    private static MetricWidgetViewModel Gpu() => new("gpu", "vram");

    [TestMethod]
    public void Cpu_widget_surfaces_only_cpu_and_ram()
    {
        var vm = Cpu();
        vm.ApplySnapshot(WithGpu());
        CollectionAssert.AreEqual(new[] { "cpu", "ram" }, vm.Rows.Select(r => r.Key).ToArray());
        Assert.AreEqual("cpu", vm.Compute!.Key);
        Assert.AreEqual("ram", vm.Memory!.Key);
        Assert.IsTrue(vm.HasContent);
    }

    [TestMethod]
    public void Gpu_widget_surfaces_only_gpu_and_vram()
    {
        var vm = Gpu();
        vm.ApplySnapshot(WithGpu());
        CollectionAssert.AreEqual(new[] { "gpu", "vram" }, vm.Rows.Select(r => r.Key).ToArray());
        Assert.AreEqual("gpu", vm.Compute!.Key);
        Assert.AreEqual("vram", vm.Memory!.Key);
    }

    [TestMethod]
    public void Gpu_widget_has_no_content_without_a_gpu()
    {
        var vm = Gpu();
        vm.ApplySnapshot(WithGpu() with { Gpu = null });
        Assert.IsEmpty(vm.Rows);
        Assert.IsFalse(vm.HasContent);
        Assert.IsNull(vm.Compute);
        Assert.IsNull(vm.Memory);
    }

    [TestMethod]
    public void Cpu_widget_keeps_content_without_a_gpu()
    {
        var vm = Cpu();
        vm.ApplySnapshot(WithGpu() with { Gpu = null });
        CollectionAssert.AreEqual(new[] { "cpu", "ram" }, vm.Rows.Select(r => r.Key).ToArray());
        Assert.IsTrue(vm.HasContent);
    }

    [TestMethod]
    public void ApplySnapshot_updates_existing_row_instances_in_place()
    {
        var vm = Cpu();
        vm.ApplySnapshot(WithGpu());
        var cpuRow = vm.Rows.Single(r => r.Key == "cpu");

        vm.ApplySnapshot(WithGpu() with { Cpu = new CpuMetrics(50.0, null, null) });

        Assert.AreSame(cpuRow, vm.Rows.Single(r => r.Key == "cpu"));
        Assert.AreEqual("50", cpuRow.Value);
    }

    [TestMethod]
    public void BindVisibility_hides_one_compute_element_without_its_siblings()
    {
        var vm = Cpu();
        vm.BindVisibility(new Dictionary<string, bool> { ["cpu.usage"] = false });
        vm.ApplySnapshot(WithGpu());

        Assert.IsFalse(vm.Compute!.UsageVisible);
        Assert.IsTrue(vm.Compute.TempVisible);
    }

    [TestMethod]
    public void RefreshVisibility_hides_whole_memory_card()
    {
        var visibility = new Dictionary<string, bool>();
        var vm = Gpu();
        vm.BindVisibility(visibility);
        vm.ApplySnapshot(WithGpu());

        visibility["vram.usage"] = false;
        vm.RefreshVisibility("vram.usage");

        Assert.IsFalse(vm.Memory!.IsVisible);
    }

    [TestMethod]
    public void RefreshVisibility_for_a_foreign_key_is_a_no_op()
    {
        var vm = Cpu();
        vm.BindVisibility(new Dictionary<string, bool>());
        vm.ApplySnapshot(WithGpu());
        vm.RefreshVisibility("gpu.power");

        CollectionAssert.AreEqual(new[] { "cpu", "ram" }, vm.Rows.Select(r => r.Key).ToArray());
    }

    [TestMethod]
    public void Hidden_metric_stays_hidden_across_snapshots()
    {
        var visibility = new Dictionary<string, bool>();
        var vm = Cpu();
        vm.BindVisibility(visibility);
        vm.ApplySnapshot(WithGpu());

        visibility["cpu.temp"] = false;
        vm.RefreshVisibility("cpu.temp");
        vm.ApplySnapshot(WithGpu());

        Assert.IsFalse(vm.Compute!.TempVisible);
    }

    [TestMethod]
    public void Reflects_external_changes_to_the_bound_visibility_map()
    {
        // Single source: the widget reads the shared map rather than copying it, so mutating the map
        // and refreshing updates the row. This is what keeps render visibility from drifting away
        // from the same map that drives device polling.
        var visibility = new Dictionary<string, bool>();
        var vm = Cpu();
        vm.BindVisibility(visibility);
        vm.ApplySnapshot(WithGpu());
        Assert.IsTrue(vm.Compute!.UsageVisible);

        visibility["cpu.usage"] = false;
        vm.RefreshVisibility("cpu.usage");

        Assert.IsFalse(vm.Compute!.UsageVisible);
    }

    [TestMethod]
    public void ApplyAppearance_sets_a_solid_brush_from_derived_color()
    {
        var vm = Cpu();
        vm.ApplyAppearance("#0F121D", 100);

        var brush = Assert.IsInstanceOfType<Avalonia.Media.SolidColorBrush>(vm.CardBackground);
        Assert.AreEqual(Avalonia.Media.Color.Parse("#FF0F121D"), brush.Color);
    }
}
