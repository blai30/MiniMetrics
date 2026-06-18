using System;
using System.Globalization;

namespace MiniMetrics.Lib;

// Pure, Avalonia-free clock/date formatting. Converts an instant into the chosen time zone and
// renders it; invariant culture keeps month and weekday names stable English regardless of locale.
public static class ClockFormatting
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // Built-in defaults, used whenever a custom format is blank. "T"/"D" are locale-aware standard
    // long-time / long-date patterns; the hover defaults force an unambiguous 24-hour local time and
    // the absolute UTC instant ("u" renders e.g. 2026-06-16 14:26:42Z).
    public const string DefaultTimeFormat = "T";
    public const string DefaultDateFormat = "D";
    public const string DefaultTimeFormatHover = "HH:mm:ss";
    public const string DefaultDateFormatHover = "u";

    // A fixed instant used only to probe whether a custom format string renders.
    private static readonly DateTimeOffset Sample = new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero);

    // Converts the instant into the zone, then renders it with customFormat when that is non-blank and
    // valid, otherwise with defaultFormat. A bad custom format can never throw out of here, so a
    // corrupt saved value cannot crash the widget.
    public static string Render(
        DateTimeOffset instant, TimeZoneInfo zone,
        string? customFormat, string defaultFormat, CultureInfo culture)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, zone);
        if (!string.IsNullOrWhiteSpace(customFormat))
        {
            try
            {
                return local.ToString(customFormat, culture);
            }
            catch (FormatException)
            {
            }
        }

        return local.ToString(defaultFormat, culture);
    }

    // True when format is blank (meaning "use the default") or renders without throwing.
    public static bool IsValidFormat(string? format, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return true;
        }

        try
        {
            Sample.ToString(format, culture);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // 12-hour "h:mm:ss tt" (2:26:42 PM) or 24-hour "HH:mm:ss" (14:26:42).
    public static string FormatTime(DateTimeOffset instant, TimeZoneInfo zone, bool use24Hour)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, zone);
        return local.ToString(use24Hour ? "HH:mm:ss" : "h:mm:ss tt", Culture);
    }

    // "dddd, MMMM d, yyyy" (Tuesday, June 16, 2026), optionally suffixed with the zone's UTC
    // offset (Tuesday, June 16, 2026  UTC-08:00).
    public static string FormatDate(DateTimeOffset instant, TimeZoneInfo zone, bool showZone = false)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, zone);
        string date = local.ToString("dddd, MMMM d, yyyy", Culture);
        return showZone ? $"{date}  {FormatZoneOffset(local.Offset)}" : date;
    }

    // "UTC-08:00" / "UTC+05:30" / "UTC+00:00".
    private static string FormatZoneOffset(TimeSpan offset)
    {
        string sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"UTC{sign}{offset.Duration().ToString("hh\\:mm", Culture)}";
    }
}
