using System;
using System.Globalization;

namespace MiniMetrics.Lib;

// Pure, Avalonia-free clock/date formatting. Converts an instant into the chosen time zone and
// renders it with the caller-supplied culture.
public static class ClockFormatting
{
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
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        if (string.IsNullOrWhiteSpace(customFormat)) return local.ToString(defaultFormat, culture);
        try
        {
            return local.ToString(customFormat, culture);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            // A malformed pattern throws FormatException; one that expands past the formatter's
            // length cap throws ArgumentOutOfRangeException. Either way, fall back to the default.
        }

        return local.ToString(defaultFormat, culture);
    }

    // True when format is blank (meaning "use the default") or renders without throwing.
    public static bool IsValidFormat(string? format, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(format)) return true;

        try
        {
            Sample.ToString(format, culture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
