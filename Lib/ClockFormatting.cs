using System;
using System.Globalization;

namespace MiniMetrics.Lib;

// Pure, Avalonia-free clock/date formatting. Converts an instant into the chosen time zone and
// renders it; invariant culture keeps month and weekday names stable English regardless of locale.
public static class ClockFormatting
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // 12-hour "h:mm:ss tt" (2:26:42 PM) or 24-hour "HH:mm:ss" (14:26:42).
    public static string FormatTime(DateTimeOffset instant, TimeZoneInfo zone, bool use24Hour)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, zone);
        return local.ToString(use24Hour ? "HH:mm:ss" : "h:mm:ss tt", Culture);
    }

    // "dddd, MMMM d, yyyy" (Tuesday, June 16, 2026).
    public static string FormatDate(DateTimeOffset instant, TimeZoneInfo zone)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, zone);
        return local.ToString("dddd, MMMM d, yyyy", Culture);
    }
}
