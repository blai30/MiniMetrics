using System.IO;
using MiniMetrics.Models;
using MiniMetrics.Services;
using MiniMetrics.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class WidgetCoordinatorTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");

    private static MetricsSnapshot Snapshot() => new(
        new CpuMetrics(34.0, null, null),
        new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL),
        new GpuMetrics(78.0, 71.0, 6_871_947_674UL, 12_884_901_888UL, 185.0));

    private static (WidgetCoordinator coordinator, SettingsController controller,
        MetricWidgetViewModel cpu, MetricWidgetViewModel gpu, RecordingSensorSource source) NewCoordinator()
    {
        var controller = new SettingsController(new Settings(), new SettingsStore(TempPath()), new FakeSaveScheduler());
        var cpu = new MetricWidgetViewModel("cpu", "ram");
        var gpu = new MetricWidgetViewModel("gpu", "vram");
        cpu.BindVisibility(controller.Current.Visibility);
        gpu.BindVisibility(controller.Current.Visibility);
        cpu.ApplySnapshot(Snapshot());
        gpu.ApplySnapshot(Snapshot());
        var source = new RecordingSensorSource();
        var coordinator = new WidgetCoordinator(controller, cpu, gpu, source);
        return (coordinator, controller, cpu, gpu, source);
    }

    [TestMethod]
    public void SetMetricVisibility_persists_the_change()
    {
        var (coordinator, controller, _, _, _) = NewCoordinator();

        coordinator.SetMetricVisibility("cpu.usage", false);

        Assert.IsFalse(controller.Current.Visibility["cpu.usage"]);
    }

    [TestMethod]
    public void SetMetricVisibility_rerenders_the_owning_widget()
    {
        var (coordinator, _, cpu, _, _) = NewCoordinator();

        coordinator.SetMetricVisibility("cpu.usage", false);

        Assert.IsFalse(cpu.Compute!.UsageVisible);
    }

    [TestMethod]
    public void SetMetricVisibility_releases_a_device_whose_metrics_are_all_hidden_but_keeps_its_sibling()
    {
        var (coordinator, _, _, _, source) = NewCoordinator();

        // cpu.temp and cpu.power ship off, so hiding cpu.usage hides the whole CPU device, while RAM
        // stays visible and keeps the memory device polled.
        coordinator.SetMetricVisibility("cpu.usage", false);

        Assert.IsFalse(source.Cpu);
        Assert.IsTrue(source.Memory);
    }

    [TestMethod]
    public void ApplyActiveDevices_releases_both_cpu_devices_for_a_hidden_cpu_widget()
    {
        var (coordinator, controller, _, _, source) = NewCoordinator();

        controller.ToggleCpuHidden();
        coordinator.ApplyActiveDevices();

        Assert.IsFalse(source.Cpu);
        Assert.IsFalse(source.Memory);
        Assert.IsTrue(source.Gpu);
    }
}
