using MiniMetrics.Models;

namespace MiniMetrics.Services;

// Reads one snapshot of current hardware metrics. Implementations may be stateful.
public interface ISensorSource
{
    MetricsSnapshot Read();
}
