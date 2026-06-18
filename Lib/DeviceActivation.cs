using System.Collections.Generic;

namespace MiniMetrics.Lib;

// Decides which physical devices the poller should refresh. A device is polled only when its
// owning widget is shown and at least one of its metrics is visible, so hiding a widget stops its
// sensors. The CPU widget owns the CPU and RAM devices; the GPU widget owns the GPU device (which
// also feeds VRAM).
public static class DeviceActivation
{
    public readonly record struct Result(bool Cpu, bool Memory, bool Gpu);

    public static Result Compute(
        IReadOnlyDictionary<string, bool> visibility,
        bool cpuWidgetShown,
        bool gpuWidgetShown)
    {
        bool CardVisible(string card) => MetricRegistry.AnyVisible(card, visibility);

        bool cpu = cpuWidgetShown && CardVisible("cpu");
        bool memory = cpuWidgetShown && CardVisible("ram");
        bool gpu = gpuWidgetShown && (CardVisible("gpu") || CardVisible("vram"));

        return new Result(cpu, memory, gpu);
    }
}
