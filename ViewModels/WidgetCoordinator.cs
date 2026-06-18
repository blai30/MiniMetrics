using MiniMetrics.Lib;
using MiniMetrics.Models;
using MiniMetrics.Services;

namespace MiniMetrics.ViewModels;

// Keeps the metric widgets and device polling in step with the persisted visibility state. A single
// visibility change has to persist, re-render the owning widget, and release any device whose metrics
// are now all hidden; running that sequence in one place is what stops a call site from doing a partial
// update that leaves the render, the polling, and the saved state out of sync.
public sealed class WidgetCoordinator
{
    private readonly SettingsController _controller;
    private readonly MetricWidgetViewModel _cpu;
    private readonly MetricWidgetViewModel _gpu;
    private readonly ISensorSource _source;

    public WidgetCoordinator(
        SettingsController controller,
        MetricWidgetViewModel cpu,
        MetricWidgetViewModel gpu,
        ISensorSource source)
    {
        _controller = controller;
        _cpu = cpu;
        _gpu = gpu;
        _source = source;
    }

    // Applies a per-metric visibility change: persist it, re-render the owning widget, and reconcile
    // which devices stay polled.
    public void SetMetricVisibility(string key, bool visible)
    {
        _controller.SetMetricVisibility(key, visible);
        _cpu.RefreshVisibility(key);
        _gpu.RefreshVisibility(key);
        ApplyActiveDevices();
    }

    // Releases any device whose widget is hidden or whose every metric is hidden, so nothing hidden is
    // polled. Safe to call after a widget show/hide as well as after a metric change.
    public void ApplyActiveDevices()
    {
        Settings settings = _controller.Current;
        DeviceActivation.Result result = DeviceActivation.Compute(
            settings.Visibility,
            cpuWidgetShown: !settings.Hidden,
            gpuWidgetShown: !settings.GpuHidden);

        _source.SetActiveDevices(result.Cpu, result.Memory, result.Gpu);
    }
}
