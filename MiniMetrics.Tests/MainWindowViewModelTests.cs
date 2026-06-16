using System.Linq;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using Xunit;

namespace MiniMetrics.Tests;

public class MainWindowViewModelTests
{
    private static MetricsSnapshot WithGpu() => new(
        new CpuMetrics(34.0, null),
        new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
        new GpuMetrics(78.0, 71.0, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    [Fact]
    public void ApplySnapshot_populates_rows()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());
        Assert.Equal(new[] { "cpu", "ram", "gpu", "vram" }, vm.Rows.Select(r => r.Key).ToArray());
        Assert.Equal("34", vm.Rows.Single(r => r.Key == "cpu").Value);
    }

    [Fact]
    public void ApplySnapshot_updates_existing_row_instances_in_place()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());
        var cpuRow = vm.Rows.Single(r => r.Key == "cpu");

        var updated = WithGpu() with { Cpu = new CpuMetrics(50.0, null) };
        vm.ApplySnapshot(updated);

        Assert.Same(cpuRow, vm.Rows.Single(r => r.Key == "cpu"));
        Assert.Equal("50", cpuRow.Value);
    }

    [Fact]
    public void ApplySnapshot_removes_gpu_rows_when_gpu_disappears()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());
        vm.ApplySnapshot(WithGpu() with { Gpu = null });
        Assert.Equal(new[] { "cpu", "ram" }, vm.Rows.Select(r => r.Key).ToArray());
    }

    [Fact]
    public void Column_accessors_map_to_the_matching_rows()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());

        Assert.Equal("cpu", vm.Cpu!.Key);
        Assert.Equal("ram", vm.Ram!.Key);
        Assert.Equal("gpu", vm.Gpu!.Key);
        Assert.Equal("vram", vm.Vram!.Key);
        Assert.True(vm.HasGpu);
    }

    [Fact]
    public void HasGpu_is_false_and_gpu_accessors_null_without_a_gpu()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu() with { Gpu = null });

        Assert.NotNull(vm.Cpu);
        Assert.Null(vm.Gpu);
        Assert.Null(vm.Vram);
        Assert.False(vm.HasGpu);
    }

    [Fact]
    public void ApplyAppearance_sets_a_solid_brush_from_derived_color()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ApplyAppearance("#0F121D", 100);

        var brush = Assert.IsType<Avalonia.Media.SolidColorBrush>(viewModel.CardBackground);
        Assert.Equal(Avalonia.Media.Color.Parse("#FF0F121D"), brush.Color);
    }
}
