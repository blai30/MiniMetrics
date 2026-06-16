using System;

namespace MiniMetrics.Lib;

public static class MetricFormatting
{
    private const double BytesPerGiB = 1024d * 1024d * 1024d;

    public static string FormatPercent(double value) => $"{Math.Round(value):0}%";

    public static string FormatGiB(ulong bytes, int decimals = 1)
        => (bytes / BytesPerGiB).ToString("F" + decimals);

    public static string FormatTemp(double celsius) => $"{Math.Round(celsius):0}°C";

    public static string FormatPower(double watts) => $"{Math.Round(watts):0}W";
}
