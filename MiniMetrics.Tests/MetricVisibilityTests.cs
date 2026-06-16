using System.Collections.Generic;
using System.Linq;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using Xunit;

namespace MiniMetrics.Tests;

public class MetricVisibilityTests
{
    private static MetricsSnapshot WithGpu() => new(
        new CpuMetrics(34.0, null),
        new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
        new GpuMetrics(78.0, 71.0, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    [Fact]
    public void LoadVisibility_hides_one_element_of_a_card_without_hiding_its_siblings()
    {
        var vm = new MainWindowViewModel();
        vm.LoadVisibility(new Dictionary<string, bool> { ["cpu.usage"] = false });
        vm.ApplySnapshot(WithGpu());

        var cpu = vm.Rows.Single(r => r.Key == "cpu");
        Assert.False(cpu.UsageVisible);
        Assert.True(cpu.TempVisible);
    }

    [Fact]
    public void SetVisibility_updates_the_owning_element_immediately()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());
        vm.SetVisibility("gpu.power", false);

        Assert.False(vm.Rows.Single(r => r.Key == "gpu").PowerVisible);
        Assert.True(vm.Rows.Single(r => r.Key == "gpu").UsageVisible);
    }

    [Fact]
    public void SetVisibility_hides_whole_memory_card()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());
        vm.SetVisibility("vram.usage", false);

        Assert.False(vm.Rows.Single(r => r.Key == "vram").IsVisible);
    }

    [Fact]
    public void Hidden_metric_stays_hidden_across_snapshots()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());
        vm.SetVisibility("cpu.temp", false);
        vm.ApplySnapshot(WithGpu());

        Assert.False(vm.Rows.Single(r => r.Key == "cpu").TempVisible);
    }
}
