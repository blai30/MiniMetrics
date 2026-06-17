using System.Collections.Generic;
using System.Linq;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using Xunit;

namespace MiniMetrics.Tests;

public class MetricWidgetViewModelTests
{
    private static MetricsSnapshot WithGpu() => new(
        new CpuMetrics(34.0, null, null),
        new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
        new GpuMetrics(78.0, 71.0, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    private static MetricWidgetViewModel Cpu() => new("cpu", "ram");
    private static MetricWidgetViewModel Gpu() => new("gpu", "vram");

    [Fact]
    public void Cpu_widget_surfaces_only_cpu_and_ram()
    {
        var vm = Cpu();
        vm.ApplySnapshot(WithGpu());
        Assert.Equal(new[] { "cpu", "ram" }, vm.Rows.Select(r => r.Key).ToArray());
        Assert.Equal("cpu", vm.Compute!.Key);
        Assert.Equal("ram", vm.Memory!.Key);
        Assert.True(vm.HasContent);
    }

    [Fact]
    public void Gpu_widget_surfaces_only_gpu_and_vram()
    {
        var vm = Gpu();
        vm.ApplySnapshot(WithGpu());
        Assert.Equal(new[] { "gpu", "vram" }, vm.Rows.Select(r => r.Key).ToArray());
        Assert.Equal("gpu", vm.Compute!.Key);
        Assert.Equal("vram", vm.Memory!.Key);
    }

    [Fact]
    public void Gpu_widget_has_no_content_without_a_gpu()
    {
        var vm = Gpu();
        vm.ApplySnapshot(WithGpu() with { Gpu = null });
        Assert.Empty(vm.Rows);
        Assert.False(vm.HasContent);
        Assert.Null(vm.Compute);
        Assert.Null(vm.Memory);
    }

    [Fact]
    public void Cpu_widget_keeps_content_without_a_gpu()
    {
        var vm = Cpu();
        vm.ApplySnapshot(WithGpu() with { Gpu = null });
        Assert.Equal(new[] { "cpu", "ram" }, vm.Rows.Select(r => r.Key).ToArray());
        Assert.True(vm.HasContent);
    }

    [Fact]
    public void ApplySnapshot_updates_existing_row_instances_in_place()
    {
        var vm = Cpu();
        vm.ApplySnapshot(WithGpu());
        var cpuRow = vm.Rows.Single(r => r.Key == "cpu");

        vm.ApplySnapshot(WithGpu() with { Cpu = new CpuMetrics(50.0, null, null) });

        Assert.Same(cpuRow, vm.Rows.Single(r => r.Key == "cpu"));
        Assert.Equal("50", cpuRow.Value);
    }

    [Fact]
    public void BindVisibility_hides_one_compute_element_without_its_siblings()
    {
        var vm = Cpu();
        vm.BindVisibility(new Dictionary<string, bool> { ["cpu.usage"] = false });
        vm.ApplySnapshot(WithGpu());

        Assert.False(vm.Compute!.UsageVisible);
        Assert.True(vm.Compute.TempVisible);
    }

    [Fact]
    public void RefreshVisibility_hides_whole_memory_card()
    {
        var visibility = new Dictionary<string, bool>();
        var vm = Gpu();
        vm.BindVisibility(visibility);
        vm.ApplySnapshot(WithGpu());

        visibility["vram.usage"] = false;
        vm.RefreshVisibility("vram.usage");

        Assert.False(vm.Memory!.IsVisible);
    }

    [Fact]
    public void RefreshVisibility_for_a_foreign_key_is_a_no_op()
    {
        var vm = Cpu();
        vm.BindVisibility(new Dictionary<string, bool>());
        vm.ApplySnapshot(WithGpu());
        vm.RefreshVisibility("gpu.power");

        Assert.Equal(new[] { "cpu", "ram" }, vm.Rows.Select(r => r.Key).ToArray());
    }

    [Fact]
    public void Hidden_metric_stays_hidden_across_snapshots()
    {
        var visibility = new Dictionary<string, bool>();
        var vm = Cpu();
        vm.BindVisibility(visibility);
        vm.ApplySnapshot(WithGpu());

        visibility["cpu.temp"] = false;
        vm.RefreshVisibility("cpu.temp");
        vm.ApplySnapshot(WithGpu());

        Assert.False(vm.Compute!.TempVisible);
    }

    [Fact]
    public void Reflects_external_changes_to_the_bound_visibility_map()
    {
        // Single source: the widget reads the shared map rather than copying it, so mutating the map
        // and refreshing updates the row. This is what keeps render visibility from drifting away
        // from the same map that drives device polling.
        var visibility = new Dictionary<string, bool>();
        var vm = Cpu();
        vm.BindVisibility(visibility);
        vm.ApplySnapshot(WithGpu());
        Assert.True(vm.Compute!.UsageVisible);

        visibility["cpu.usage"] = false;
        vm.RefreshVisibility("cpu.usage");

        Assert.False(vm.Compute!.UsageVisible);
    }

    [Fact]
    public void ApplyAppearance_sets_a_solid_brush_from_derived_color()
    {
        var vm = Cpu();
        vm.ApplyAppearance("#0F121D", 100);

        var brush = Assert.IsType<Avalonia.Media.SolidColorBrush>(vm.CardBackground);
        Assert.Equal(Avalonia.Media.Color.Parse("#FF0F121D"), brush.Color);
    }
}
