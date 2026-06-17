using System.Collections.Generic;
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

    // Records the base color and opacity, persisting on the debounce so dragging the slider writes once.
    public void SetAppearance(string backgroundColor, int opacity)
    {
        _settings.BackgroundColor = backgroundColor;
        _settings.Opacity = opacity;
        ScheduleSave();
    }

    // Records the chosen time zone id, persisting on the debounce.
    public void SetTimeZone(string? timeZoneId)
    {
        _settings.TimeZoneId = timeZoneId;
        ScheduleSave();
    }

    public void SetCpuPosition(int x, int y) { _settings.X = x; _settings.Y = y; ScheduleSave(); }
    public void SetGpuPosition(int x, int y) { _settings.GpuX = x; _settings.GpuY = y; ScheduleSave(); }
    public void SetDateTimePosition(int x, int y) { _settings.DateTimeX = x; _settings.DateTimeY = y; ScheduleSave(); }

    // Persists any pending debounced change immediately. Used after an on-screen correction and at
    // shutdown so nothing is lost between the last edit and the next timer tick.
    public void Flush() => _scheduler.Flush();

    private void Persist() => _store.Save(_settings);

    private void ScheduleSave() => _scheduler.Schedule(Persist);

    // Expands the legacy whole-card visibility keys (cpu/ram/gpu/vram) into the per-metric keys so
    // settings saved before per-metric visibility existed keep their hidden cards hidden.
    private void MigrateVisibility()
    {
        Dictionary<string, bool> visibility = _settings.Visibility;

        void Expand(string legacy, params string[] keys)
        {
            if (visibility.TryGetValue(legacy, out bool value))
            {
                foreach (string key in keys)
                {
                    if (!visibility.ContainsKey(key))
                    {
                        visibility[key] = value;
                    }
                }

                visibility.Remove(legacy);
            }
        }

        Expand("cpu", "cpu.usage", "cpu.temp", "cpu.power");
        Expand("ram", "ram.usage");
        Expand("gpu", "gpu.usage", "gpu.temp", "gpu.power");
        Expand("vram", "vram.usage");
    }
}
