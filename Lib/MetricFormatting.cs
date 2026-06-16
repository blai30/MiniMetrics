using System;

namespace MiniMetrics.Lib;

public static class MetricFormatting
{
    private const double BytesPerGiB = 1024d * 1024d * 1024d;

    // Bare rounded percentage (no "%"); the unit is rendered separately so it can be styled smaller.
    public static string FormatPercent(double value) => $"{Math.Round(value):0}";

    public static string FormatGiB(ulong bytes, int decimals = 1)
        => (bytes / BytesPerGiB).ToString("F" + decimals);

    // Bare rounded temperature (no "°C"); the unit is rendered separately with a superscript degree.
    public static string FormatTempValue(double celsius) => $"{Math.Round(celsius):0}";

    public static string FormatPower(double watts) => $"{Math.Round(watts):0}W";
}
