using System;
using System.Globalization;

namespace MiniMetrics.Lib;

public static class MetricFormatting
{
    private const double BytesPerGiB = 1024d * 1024d * 1024d;

    // The displayed units ("%", "GB", "W") are hardcoded English, so the numbers render invariantly too
    // rather than picking up a comma decimal separator or a non-ASCII minus from the machine's locale.

    // Bare rounded percentage (no "%"); the unit is rendered separately so it can be styled smaller.
    public static string FormatPercent(double value) => Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

    public static string FormatGiB(ulong bytes, int decimals = 1)
        => (bytes / BytesPerGiB).ToString("F" + decimals, CultureInfo.InvariantCulture);

    // Bare rounded temperature (no "°C"); the unit is rendered separately with a superscript degree.
    public static string FormatTempValue(double celsius) =>
        Math.Round(celsius).ToString("0", CultureInfo.InvariantCulture);

    public static string FormatPower(double watts) =>
        $"{Math.Round(watts).ToString("0", CultureInfo.InvariantCulture)} W";
}
