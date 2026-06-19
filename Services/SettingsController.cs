using System;
using System.Collections.Generic;
using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.Services;

// Owns the live Settings and is the single place it is mutated and persisted. Toggles and metric
// visibility write through immediately; high-frequency edits (position drags, appearance, time zone)
// are debounced via the injected scheduler. Callers read Current to project state onto their windows
// but never mutate Settings directly.
public sealed class SettingsController
{
    private readonly Settings _settings;
    private readonly SettingsStore _store;
    private readonly ISaveScheduler _scheduler;

    public SettingsController(Settings settings, SettingsStore store, ISaveScheduler scheduler)
    {
        _settings = settings;
        _store = store;
        _scheduler = scheduler;
        MigrateVisibility();
        SeedElevationDefaults();
    }

    // The live settings, for read-only projection by callers.
    public Settings Current => _settings;

    // Each toggle flips its flag, persists immediately, and returns the new value so the caller can
    // project it onto the affected windows and tray checkmark.
    public bool ToggleCpuHidden() { _settings.Hidden = !_settings.Hidden; Persist(); return _settings.Hidden; }
    public bool ToggleGpuHidden() { _settings.GpuHidden = !_settings.GpuHidden; Persist(); return _settings.GpuHidden; }
    public bool ToggleDateTimeHidden() { _settings.DateTimeHidden = !_settings.DateTimeHidden; Persist(); return _settings.DateTimeHidden; }
    public bool ToggleLocked() { _settings.Locked = !_settings.Locked; Persist(); return _settings.Locked; }
    public bool ToggleAlwaysOnTop() { _settings.AlwaysOnTop = !_settings.AlwaysOnTop; Persist(); return _settings.AlwaysOnTop; }
    public bool ToggleSnapToEdges() { _settings.SnapToEdges = !_settings.SnapToEdges; Persist(); return _settings.SnapToEdges; }

    // Sets one per-metric visibility flag and persists immediately.
    public void SetMetricVisibility(string key, bool visible)
    {
        _settings.Visibility[key] = visible;
        Persist();
    }

    // Records the base color and opacity for one theme, persisting on the debounce so dragging the
    // slider writes once. The widget background is stored per theme so each keeps its own color.
    public void SetAppearance(bool targetIsDark, string backgroundColor, int opacity)
    {
        if (targetIsDark)
        {
            _settings.BackgroundColor = backgroundColor;
        }
        else
        {
            _settings.LightBackgroundColor = backgroundColor;
        }

        _settings.Opacity = opacity;
        ScheduleSave();
    }

    // Records the chosen theme, writing through immediately like the other discrete toggles.
    public void SetTheme(AppTheme theme)
    {
        _settings.Theme = theme;
        Persist();
    }

    // Each compact toggle flips one widget's layout flag and persists immediately, matching the other
    // discrete display toggles.
    public void SetCpuCompact(bool compact) { _settings.CpuCompact = compact; Persist(); }
    public void SetGpuCompact(bool compact) { _settings.GpuCompact = compact; Persist(); }
    public void SetDateTimeCompact(bool compact) { _settings.DateTimeCompact = compact; Persist(); }

    // Records the chosen time zone id, persisting on the debounce.
    public void SetTimeZone(string? timeZoneId)
    {
        _settings.TimeZoneId = timeZoneId;
        ScheduleSave();
    }

    // Records the four clock format strings (null or blank means "use the built-in default" for that
    // line), persisting on the debounce so a burst of edits writes once.
    public void SetClockFormats(string? time, string? date, string? timeHover, string? dateHover)
    {
        _settings.ClockTimeFormat = time;
        _settings.ClockDateFormat = date;
        _settings.ClockTimeFormatHover = timeHover;
        _settings.ClockDateFormatHover = dateHover;
        ScheduleSave();
    }

    // Records the chosen clock locale id (null = machine current culture), persisting on the debounce.
    public void SetClockLocale(string? localeId)
    {
        _settings.ClockLocaleId = localeId;
        ScheduleSave();
    }

    public void SetCpuPosition(int x, int y) { _settings.X = x; _settings.Y = y; ScheduleSave(); }
    public void SetGpuPosition(int x, int y) { _settings.GpuX = x; _settings.GpuY = y; ScheduleSave(); }
    public void SetDateTimePosition(int x, int y) { _settings.DateTimeX = x; _settings.DateTimeY = y; ScheduleSave(); }

    // Records whether the launch-time update check runs and how often, persisting on the debounce so a
    // burst of settings toggles writes once.
    public void SetUpdatePreferences(bool enabled, UpdateCheckFrequency frequency)
    {
        _settings.UpdateCheckEnabled = enabled;
        _settings.UpdateFrequency = frequency;
        ScheduleSave();
    }

    // Stamps the time of the last successful update check; persisted immediately so the cadence gate
    // survives a crash before the next debounce.
    public void SetLastUpdateCheck(DateTimeOffset utc)
    {
        _settings.LastUpdateCheckUtc = utc;
        Persist();
    }

    // Records the version the user chose to skip; persisted immediately so the suppression survives a
    // restart.
    public void SetSkippedUpdateVersion(string version)
    {
        _settings.SkippedUpdateVersion = version;
        Persist();
    }

    // Persists any pending debounced change immediately. Used after an on-screen correction and at
    // shutdown so nothing is lost between the last edit and the next timer tick.
    public void Flush() => _scheduler.Flush();

    private void Persist() => _store.Save(_settings);

    private void ScheduleSave() => _scheduler.Schedule(Persist);

    // Expands the legacy whole-card visibility keys (cpu/ram/gpu/vram) into the per-metric keys so
    // settings saved before per-metric visibility existed keep their hidden cards hidden. The card
    // keys and their metrics come from the registry.
    private void MigrateVisibility()
    {
        Dictionary<string, bool> visibility = _settings.Visibility;

        foreach (string card in MetricRegistry.Cards)
        {
            if (!visibility.TryGetValue(card, out bool value))
            {
                continue;
            }

            foreach (MetricEntry entry in MetricRegistry.ForCard(card))
            {
                if (!visibility.ContainsKey(entry.Key))
                {
                    visibility[entry.Key] = value;
                }
            }

            visibility.Remove(card);
        }
    }

    // CPU temperature and power ship off, so a fresh install never asks for administrator rights until
    // the user opts in. Only seed when the key is absent, so a saved or migrated value always wins.
    private void SeedElevationDefaults()
    {
        foreach (MetricEntry entry in MetricRegistry.All)
        {
            if (entry.RequiresElevation && !_settings.Visibility.ContainsKey(entry.Key))
            {
                _settings.Visibility[entry.Key] = false;
            }
        }
    }
}
