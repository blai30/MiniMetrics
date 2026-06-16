namespace MiniMetrics.Models;

// The full set of readings produced once per poll. Any section is null when its device is absent
// or has been released because all of its metrics are hidden.
public sealed record MetricsSnapshot(
    CpuMetrics? Cpu,
    MemoryMetrics? Memory,
    GpuMetrics? Gpu);

// TempCelsius is null in v1 because CPU temperature requires the kernel driver, which is deferred.
public sealed record CpuMetrics(double UsagePercent, double? TempCelsius);

public sealed record MemoryMetrics(ulong UsedBytes, ulong TotalBytes);

public sealed record GpuMetrics(
    double UsagePercent,
    double TempCelsius,
    ulong VramUsedBytes,
    ulong VramTotalBytes,
    double PowerWatts);
