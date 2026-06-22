using System;
using System.Collections.Generic;
using System.Globalization;
using MiniMetrics.Lib;
using MiniMetrics.Models;
using MiniMetrics.Services;

namespace MiniMetrics.ViewModels;

// Owns what one settings change implies: persist it through the controller and reflect it onto the live
// widgets. Each setting used to be routed twice, once to a controller Set* call and once to an apply
// step scattered across the host; keeping both halves here means a setting takes effect in one place and
// adding one is one match arm, not a controller method plus a host arm kept in sync by hand.
//
// Metric visibility is deliberately not handled here: enabling an elevation-flagged metric can relaunch
// the process or prompt for a driver, an outcome only the host can render, so that one stays with the host.
public sealed class SettingsApplier
{
    private readonly SettingsController _controller;
    private readonly MetricWidgetViewModel _cpu;
    private readonly MetricWidgetViewModel _gpu;
    private readonly DateTimeWidgetViewModel _dateTime;
    private readonly IReadOnlyList<IWidgetDisplay> _widgets;

    // The two effects that are the application's, not a widget's: requesting the Avalonia theme variant
    // from the saved theme, and reporting the resolved light/dark variant the host derives from it.
    private readonly Action _applyThemeVariant;
    private readonly Func<bool> _resolvedIsDark;

    public SettingsApplier(
        SettingsController controller,
        MetricWidgetViewModel cpu,
        MetricWidgetViewModel gpu,
        DateTimeWidgetViewModel dateTime,
        IReadOnlyList<IWidgetDisplay> widgets,
        Action applyThemeVariant,
        Func<bool> resolvedIsDark)
    {
        _controller = controller;
        _cpu = cpu;
        _gpu = gpu;
        _dateTime = dateTime;
        _widgets = widgets;
        _applyThemeVariant = applyThemeVariant;
        _resolvedIsDark = resolvedIsDark;
    }

    // Routes one settings change to its persistence and its live effect. Metric visibility is intercepted
    // by the host before this point, so it has no arm here.
    public void Apply(SettingChange change)
    {
        switch (change)
        {
            case SettingChange.Appearance appearance:
                _controller.SetAppearance(appearance.IsDark, appearance.Color, appearance.Opacity);
                ApplyAppearance();
                break;
            case SettingChange.Theme theme:
                _controller.SetTheme(theme.Value);
                _applyThemeVariant();
                ApplyAppearance();
                RefreshAccents();
                break;
            case SettingChange.Compact compact:
                ApplyCompact(compact.Widget, compact.IsCompact);
                break;
            case SettingChange.Alignment alignment:
                _controller.SetClockAlignment(alignment.Value);
                _dateTime.SetAlignment(alignment.Value);
                break;
            case SettingChange.TimeZone timeZone:
                // ResolveTimeZone(null) maps a null id back to the machine zone, matching the startup path.
                _controller.SetTimeZone(timeZone.ZoneId);
                _dateTime.SetTimeZone(ResolveTimeZone(timeZone.ZoneId));
                break;
            case SettingChange.ClockFormats formats:
                _controller.SetClockFormats(formats.Time, formats.Date, formats.TimeHover, formats.DateHover);
                _dateTime.SetFormats(formats.Time, formats.Date, formats.TimeHover, formats.DateHover);
                break;
            case SettingChange.ClockLocale locale:
                _controller.SetClockLocale(locale.Locale.Name);
                _dateTime.SetLocale(locale.Locale);
                break;
            case SettingChange.UpdatePreferences updates:
                _controller.SetUpdatePreferences(updates.Enabled, updates.Frequency);
                break;
            case SettingChange.WidgetStyle style:
                _controller.SetWidgetStyle(style.Family, style.Scale, style.Weight);
                ApplyStyle();
                break;
        }
    }

    // Pushes the current opacity and the resolved theme's background color to every widget through the
    // shared appearance seam. Also called by the host at startup and when the OS theme flips under System.
    public void ApplyAppearance()
    {
        var settings = _controller.Current;
        string background = _resolvedIsDark() ? settings.BackgroundColor : settings.LightBackgroundColor;
        foreach (var widget in _widgets) widget.ApplyAppearance(background, settings.Opacity);
    }

    // Resolves one style profile from the current settings and pushes it to every widget through the
    // shared widget-style seam. Also called by the host at startup.
    public void ApplyStyle()
    {
        var settings = _controller.Current;
        var profile = WidgetStyleProfile.Resolve(
            settings.WidgetFontFamily, settings.WidgetScale, settings.WidgetFontWeight);
        foreach (var widget in _widgets) widget.ApplyStyle(profile);
    }

    // Re-raises accent colors on the metric widgets so the theme-aware converters re-run. Also called by
    // the host when the OS theme flips under System.
    public void RefreshAccents()
    {
        _cpu.RefreshThemeColors();
        _gpu.RefreshThemeColors();
    }

    // Persists a per-widget compact toggle and pushes it onto the affected widget so it reflows live.
    private void ApplyCompact(string widget, bool value)
    {
        switch (widget)
        {
            case "cpu":
                _controller.SetCpuCompact(value);
                _cpu.IsCompact = value;
                break;
            case "gpu":
                _controller.SetGpuCompact(value);
                _gpu.IsCompact = value;
                break;
            case "clock":
                _controller.SetDateTimeCompact(value);
                _dateTime.IsCompact = value;
                break;
        }
    }

    // Resolves a saved zone id to a TimeZoneInfo, falling back to local if it is missing or unknown on
    // this machine. Shared by the startup seed and the time-zone change.
    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrEmpty(id)) return TimeZoneInfo.Local;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    // Resolves a saved locale id to a CultureInfo, falling back to the machine's current culture if it is
    // missing or unknown on this machine. Shared by the startup seed and the locale change.
    public static CultureInfo ResolveLocale(string? id)
    {
        if (string.IsNullOrEmpty(id)) return CultureInfo.CurrentCulture;

        try
        {
            return CultureInfo.GetCultureInfo(id);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentCulture;
        }
    }
}
