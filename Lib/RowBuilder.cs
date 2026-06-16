using System.Collections.Generic;
using MiniMetrics.Models;

namespace MiniMetrics.Lib;

public enum RowColor
{
    Cyan,
    Green,
    Amber,
    Violet,
}

// Health band for a temperature reading, used to color-code the temperature text.
public enum TempLevel
{
    None,
    Cool,
    Warm,
    Hot,
    Critical,
}

// A single display row. Value is the bold primary number. Temp is an optional color-coded
// temperature. Detail is passive muted trailing text (power draw, or the memory total).
public sealed record MetricRow(
    string Key,
    string Label,
    string Value,
    string Temp,
    TempLevel TempLevel,
    string Detail,
    double BarPercent,
    RowColor Color);

public static class RowBuilder
{
    public static IReadOnlyList<MetricRow> Build(MetricsSnapshot snapshot)
    {
        var rows = new List<MetricRow>();

        if (snapshot.Cpu is { } cpu)
        {
            rows.Add(new(
                "cpu",
                "CPU",
                MetricFormatting.FormatPercent(cpu.UsagePercent),
                // CPU temperature is deferred (needs the kernel driver). Until then the hero slot
                // shows a muted placeholder rather than going blank.
                cpu.TempCelsius is { } cpuTemp ? MetricFormatting.FormatTempValue(cpuTemp) : "—",
                cpu.TempCelsius is { } cpuLevel ? LevelFor(cpuLevel) : TempLevel.None,
                // CPU power is deferred (needs the kernel driver). Until then the detail slot shows a
                // muted placeholder rather than a misleading reading.
                cpu.PowerWatts is { } cpuPower ? MetricFormatting.FormatPower(cpuPower) : "—",
                cpu.UsagePercent,
                RowColor.Cyan));
        }

        if (snapshot.Memory is { } memory)
        {
            rows.Add(new(
                "ram",
                "RAM",
                MetricFormatting.FormatGiB(memory.UsedBytes),
                "",
                TempLevel.None,
                $"/ {MetricFormatting.FormatGiB(memory.TotalBytes, 0)} GB",
                Percent(memory.UsedBytes, memory.TotalBytes),
                RowColor.Green));
        }

        if (snapshot.Gpu is not { } gpu) return rows;
        rows.Add(new(
            "gpu",
            "GPU",
            MetricFormatting.FormatPercent(gpu.UsagePercent),
            MetricFormatting.FormatTempValue(gpu.TempCelsius),
            LevelFor(gpu.TempCelsius),
            MetricFormatting.FormatPower(gpu.PowerWatts),
            gpu.UsagePercent,
            RowColor.Amber));
        rows.Add(new(
            "vram",
            "VRAM",
            MetricFormatting.FormatGiB(gpu.VramUsedBytes),
            "",
            TempLevel.None,
            $"/ {MetricFormatting.FormatGiB(gpu.VramTotalBytes, 0)} GB",
            Percent(gpu.VramUsedBytes, gpu.VramTotalBytes),
            RowColor.Violet));

        return rows;
    }

    private static double Percent(ulong used, ulong total)
        => total == 0 ? 0 : (double)used / total * 100.0;

    // Thresholds tuned for GPU load temperatures; revisited when CPU temp and per-sensor
    // customization arrive in a later plan.
    private static TempLevel LevelFor(double celsius)
        => celsius >= 80 ? TempLevel.Critical
            : celsius >= 70 ? TempLevel.Hot
            : celsius >= 60 ? TempLevel.Warm
            : TempLevel.Cool;
}
