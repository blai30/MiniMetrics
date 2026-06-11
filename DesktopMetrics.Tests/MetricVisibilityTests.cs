using System.Collections.Generic;
using System.Linq;
using DesktopMetrics.Models;
using DesktopMetrics.ViewModels;
using Xunit;

namespace DesktopMetrics.Tests;

public class MetricVisibilityTests
{
    private static MetricsSnapshot WithGpu() => new(
        new CpuMetrics(34.0, null),
        new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
        new GpuMetrics(78.0, 71.0, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    [Fact]
    public void LoadVisibility_hides_metric_on_next_snapshot()
    {
        var vm = new MainWindowViewModel();
        vm.LoadVisibility(new Dictionary<string, bool> { ["gpu"] = false });
        vm.ApplySnapshot(WithGpu());

        Assert.False(vm.Rows.Single(r => r.Key == "gpu").IsVisible);
        Assert.True(vm.Rows.Single(r => r.Key == "cpu").IsVisible);
    }

    [Fact]
    public void SetVisibility_updates_existing_row_immediately()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());
        vm.SetVisibility("cpu", false);

        Assert.False(vm.Rows.Single(r => r.Key == "cpu").IsVisible);
    }

    [Fact]
    public void Hidden_metric_stays_hidden_across_snapshots()
    {
        var vm = new MainWindowViewModel();
        vm.ApplySnapshot(WithGpu());
        vm.SetVisibility("ram", false);
        vm.ApplySnapshot(WithGpu());

        Assert.False(vm.Rows.Single(r => r.Key == "ram").IsVisible);
    }
}
