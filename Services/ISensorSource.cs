using MiniMetrics.Models;

namespace MiniMetrics.Services;

// Reads one snapshot of current hardware metrics. Implementations may be stateful.
public interface ISensorSource
{
    MetricsSnapshot Read();

    // Selects which devices to poll. A device whose every metric is hidden is released so its
    // sensors stop refreshing; Read() then returns null for that device's section.
    void SetActiveDevices(bool cpu, bool memory, bool gpu);
}
