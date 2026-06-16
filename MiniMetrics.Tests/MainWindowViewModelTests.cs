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
        Assert.Equal("34%", vm.Rows.Single(r => r.Key == "cpu").Value);
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
        Assert.Equal("50%", cpuRow.Value);
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
    public void ApplyAppearance_sets_a_gradient_brush_from_derived_stops()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ApplyAppearance("#0F121D", 100);

        var brush = Assert.IsType<Avalonia.Media.LinearGradientBrush>(viewModel.CardBackground);
        Assert.Equal(2, brush.GradientStops.Count);
        Assert.Equal(Avalonia.Media.Color.Parse("#FF141827"), brush.GradientStops[0].Color);
        Assert.Equal(Avalonia.Media.Color.Parse("#FF0B0D15"), brush.GradientStops[1].Color);
    }
}
