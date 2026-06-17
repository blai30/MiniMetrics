using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _backgroundColor;

    [ObservableProperty]
    private int _opacity;

    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();

    [ObservableProperty]
    private TimeZoneInfo _selectedTimeZone;

    // The metric visibility checkboxes, grouped by card, built from the registry.
    public IReadOnlyList<MetricGroupViewModel> MetricGroups { get; }

    private readonly Dictionary<string, MetricToggleViewModel> _togglesByKey = new();

    public SettingsViewModel(Settings settings)
    {
        // Seed each per-metric toggle, falling back to the legacy whole-card key when the granular
        // one has not been saved yet, so an existing hidden card stays hidden after upgrading.
        bool Seed(string key, string legacy) =>
            settings.Visibility.TryGetValue(key, out bool value)
                ? value
                : settings.Visibility.GetValueOrDefault(legacy, true);

        _backgroundColor = settings.BackgroundColor;
        _opacity = settings.Opacity;

        var groups = new List<MetricGroupViewModel>();
        foreach (string card in MetricRegistry.Cards)
        {
            var toggles = new List<MetricToggleViewModel>();
            foreach (MetricEntry entry in MetricRegistry.ForCard(card))
            {
                var toggle = new MetricToggleViewModel(
                    entry.Key,
                    entry.Label,
                    Seed(entry.Key, card),
                    (key, value) => MetricVisibilityChanged?.Invoke(key, value));
                toggles.Add(toggle);
                _togglesByKey[entry.Key] = toggle;
            }

            groups.Add(new MetricGroupViewModel(card.ToUpperInvariant(), toggles));
        }

        MetricGroups = groups;
        _selectedTimeZone = ResolveZone(settings.TimeZoneId, TimeZones);
    }

    // Raised when the base color or opacity changes (live preview + persist).
    public event Action? AppearanceChanged;

    // Raised when a single metric toggle changes, with its key and new value.
    public event Action<string, bool>? MetricVisibilityChanged;

    // Raised when the chosen time zone changes (persist + live clock update).
    public event Action? TimeZoneChanged;

    // The toggle for a metric key.
    public MetricToggleViewModel ToggleFor(string key) => _togglesByKey[key];

    [RelayCommand]
    private void SelectPreset(string hex) => BackgroundColor = hex;

    partial void OnBackgroundColorChanged(string value) => AppearanceChanged?.Invoke();

    partial void OnOpacityChanged(int value) => AppearanceChanged?.Invoke();

    partial void OnSelectedTimeZoneChanged(TimeZoneInfo value) => TimeZoneChanged?.Invoke();

    // Picks the saved zone by id, else the machine's local zone (matched from the list so the
    // dropdown highlights it), else local as a last resort.
    private static TimeZoneInfo ResolveZone(string? id, IReadOnlyList<TimeZoneInfo> zones)
    {
        string targetId = id ?? TimeZoneInfo.Local.Id;
        foreach (TimeZoneInfo zone in zones)
        {
            if (zone.Id == targetId)
            {
                return zone;
            }
        }

        foreach (TimeZoneInfo zone in zones)
        {
            if (zone.Id == TimeZoneInfo.Local.Id)
            {
                return zone;
            }
        }

        // Last resort if the system list somehow lacks the local zone; the dropdown shows no
        // selection in that case.
        return TimeZoneInfo.Local;
    }
}
