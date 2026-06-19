using MiniMetrics.Models;

namespace MiniMetrics.ViewModels;

// Which setting facet changed. The host routes on this instead of subscribing to a separate event per
// setting; adding a setting adds a kind here, not a new event.
public enum SettingKind
{
    Appearance,
    Theme,
    MetricVisibility,
    Compact,
    ClockAlignment,
    TimeZone,
    ClockFormats,
    ClockLocale,
    UpdatePreferences
}

// One settings change. For the per-key facets (metric visibility, compact toggles, clock alignment) it
// carries the payload; for the rest the host reads the current value back off the view model. The
// factory methods keep call sites from having to remember which fields a kind uses.
public readonly record struct SettingChange(
    SettingKind Kind,
    string? Key = null,
    bool Flag = false,
    ClockAlignment Alignment = default)
{
    public static SettingChange Of(SettingKind kind) => new(kind);

    public static SettingChange Metric(string key, bool visible) => new(SettingKind.MetricVisibility, key, visible);

    public static SettingChange Compact(string widget, bool compact) => new(SettingKind.Compact, widget, compact);

    public static SettingChange ForAlignment(ClockAlignment alignment) =>
        new(SettingKind.ClockAlignment, Alignment: alignment);
}
