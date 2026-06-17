namespace MiniMetrics.Models;

// The full set of readings produced once per poll. Any section is null when its device is absent
// or has been released because all of its metrics are hidden.
public sealed record MetricsSnapshot(
    CpuMetrics? Cpu,
    MemoryMetrics? Memory,
    GpuMetrics? Gpu);

// TempCelsius and PowerWatts are null when the PawnIO kernel driver cannot supply a reading: it is not
// installed, or the process is not elevated to open its device.
public sealed record CpuMetrics(double UsagePercent, double? TempCelsius, double? PowerWatts);

public sealed record MemoryMetrics(ulong UsedBytes, ulong TotalBytes);

public sealed record GpuMetrics(
    double UsagePercent,
    double TempCelsius,
    ulong VramUsedBytes,
    ulong VramTotalBytes,
    double PowerWatts);
