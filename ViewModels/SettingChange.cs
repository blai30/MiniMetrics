using System.Globalization;
using MiniMetrics.Models;

namespace MiniMetrics.ViewModels;

// One settings change, carrying everything the host needs to persist and apply it. Each facet is its
// own record, so the host pattern-matches on the type and reads the payload directly instead of reaching
// back into the view model for the current value. The host routes on a single channel; adding a setting
// adds a record here and a match arm in the host, not a new event.
public abstract record SettingChange
{
    private SettingChange()
    {
    }

    // Background color or opacity changed for the variant currently being edited.
    public sealed record Appearance(bool IsDark, string Color, int Opacity) : SettingChange;

    public sealed record Theme(AppTheme Value) : SettingChange;

    public sealed record MetricVisibility(string Key, bool Visible) : SettingChange;

    public sealed record Compact(string Widget, bool IsCompact) : SettingChange;

    public sealed record Alignment(ClockAlignment Value) : SettingChange;

    // Resolved time zone id, or null for the machine's local zone.
    public sealed record TimeZone(string? ZoneId) : SettingChange;

    public sealed record ClockFormats(string? Time, string? Date, string? TimeHover, string? DateHover) : SettingChange;

    public sealed record ClockLocale(CultureInfo Locale) : SettingChange;

    public sealed record UpdatePreferences(bool Enabled, UpdateCheckFrequency Frequency) : SettingChange;

    public sealed record WidgetStyle(string Widget, string Family, int Scale, WidgetFontWeight Weight) : SettingChange;
}
