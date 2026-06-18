using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    [ObservableProperty]
    private bool _updateCheckEnabled;

    [ObservableProperty]
    private UpdateCheckFrequency _updateFrequency;

    [ObservableProperty]
    private bool _useLocalTime;

    // The full set of specific cultures for the locale picker, ordered by display name.
    public IReadOnlyList<CultureInfo> Locales { get; } =
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .OrderBy(culture => culture.DisplayName, StringComparer.CurrentCulture)
            .ToArray();

    [ObservableProperty]
    private CultureInfo _selectedLocale;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeSample))]
    [NotifyPropertyChangedFor(nameof(TimeFormatError))]
    private string? _clockTimeFormat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateSample))]
    [NotifyPropertyChangedFor(nameof(DateFormatError))]
    private string? _clockDateFormat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeHoverSample))]
    [NotifyPropertyChangedFor(nameof(TimeHoverFormatError))]
    private string? _clockTimeFormatHover;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateHoverSample))]
    [NotifyPropertyChangedFor(nameof(DateHoverFormatError))]
    private string? _clockDateFormatHover;

    // A fixed instant so the settings preview stays stable while the user edits.
    private static readonly DateTimeOffset PreviewInstant = new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero);

    // The zone used for previews mirrors the clock's resolved zone selection.
    private TimeZoneInfo PreviewZone => UseLocalTime ? TimeZoneInfo.Local : SelectedTimeZone;

    public string TimeSample => ClockFormatting.Render(
        PreviewInstant, PreviewZone, ClockTimeFormat, ClockFormatting.DefaultTimeFormat, SelectedLocale);

    public string DateSample => ClockFormatting.Render(
        PreviewInstant, PreviewZone, ClockDateFormat, ClockFormatting.DefaultDateFormat, SelectedLocale);

    public string TimeHoverSample => ClockFormatting.Render(
        PreviewInstant, PreviewZone, ClockTimeFormatHover, ClockFormatting.DefaultTimeFormatHover, SelectedLocale);

    public string DateHoverSample => ClockFormatting.Render(
        PreviewInstant, PreviewZone, ClockDateFormatHover, ClockFormatting.DefaultDateFormatHover, SelectedLocale);

    public string TimeFormatError => FormatError(ClockTimeFormat);
    public string DateFormatError => FormatError(ClockDateFormat);
    public string TimeHoverFormatError => FormatError(ClockTimeFormatHover);
    public string DateHoverFormatError => FormatError(ClockDateFormatHover);

    private string FormatError(string? format) =>
        ClockFormatting.IsValidFormat(format, SelectedLocale) ? "" : "Invalid format";

    public IReadOnlyList<UpdateCheckFrequency> UpdateFrequencies { get; } = new[]
    {
        UpdateCheckFrequency.EveryLaunch,
        UpdateCheckFrequency.Daily,
        UpdateCheckFrequency.Weekly,
        UpdateCheckFrequency.Monthly,
    };

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
        _updateCheckEnabled = settings.UpdateCheckEnabled;
        _updateFrequency = settings.UpdateFrequency;
        _useLocalTime = settings.TimeZoneId is null;
        _clockTimeFormat = settings.ClockTimeFormat;
        _clockDateFormat = settings.ClockDateFormat;
        _clockTimeFormatHover = settings.ClockTimeFormatHover;
        _clockDateFormatHover = settings.ClockDateFormatHover;

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
        _selectedLocale = ResolveLocale(settings.ClockLocaleId, Locales);
    }

    // Raised when the base color or opacity changes (live preview + persist).
    public event Action? AppearanceChanged;

    // Raised when a single metric toggle changes, with its key and new value.
    public event Action<string, bool>? MetricVisibilityChanged;

    // Raised when the chosen time zone changes (persist + live clock update).
    public event Action? TimeZoneChanged;

    // Raised when any of the four clock format strings changes (persist + live clock update).
    public event Action? ClockFormatsChanged;

    // Raised when the chosen clock locale changes (persist + live clock update).
    public event Action? ClockLocaleChanged;

    // Raised when the update-check enabled flag or cadence changes (persist).
    public event Action? UpdatePreferencesChanged;

    // The toggle for a metric key.
    public MetricToggleViewModel ToggleFor(string key) => _togglesByKey[key];

    [RelayCommand]
    private void SelectPreset(string hex) => BackgroundColor = hex;

    partial void OnBackgroundColorChanged(string value) => AppearanceChanged?.Invoke();

    partial void OnOpacityChanged(int value) => AppearanceChanged?.Invoke();

    partial void OnSelectedTimeZoneChanged(TimeZoneInfo value) => TimeZoneChanged?.Invoke();

    partial void OnUseLocalTimeChanged(bool value) => TimeZoneChanged?.Invoke();

    partial void OnClockTimeFormatChanged(string? value) => ClockFormatsChanged?.Invoke();

    partial void OnClockDateFormatChanged(string? value) => ClockFormatsChanged?.Invoke();

    partial void OnClockTimeFormatHoverChanged(string? value) => ClockFormatsChanged?.Invoke();

    partial void OnClockDateFormatHoverChanged(string? value) => ClockFormatsChanged?.Invoke();

    partial void OnSelectedLocaleChanged(CultureInfo value)
    {
        // The samples and errors all depend on the locale, so refresh every one, then notify the host.
        OnPropertyChanged(nameof(TimeSample));
        OnPropertyChanged(nameof(DateSample));
        OnPropertyChanged(nameof(TimeHoverSample));
        OnPropertyChanged(nameof(DateHoverSample));
        OnPropertyChanged(nameof(TimeFormatError));
        OnPropertyChanged(nameof(DateFormatError));
        OnPropertyChanged(nameof(TimeHoverFormatError));
        OnPropertyChanged(nameof(DateHoverFormatError));
        ClockLocaleChanged?.Invoke();
    }

    partial void OnUpdateCheckEnabledChanged(bool value) => UpdatePreferencesChanged?.Invoke();

    partial void OnUpdateFrequencyChanged(UpdateCheckFrequency value) => UpdatePreferencesChanged?.Invoke();

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

    // Picks the saved culture by name, else the machine's current culture (matched from the list so
    // the dropdown highlights it), else current culture as a last resort.
    private static CultureInfo ResolveLocale(string? id, IReadOnlyList<CultureInfo> locales)
    {
        string targetName = id ?? CultureInfo.CurrentCulture.Name;
        foreach (CultureInfo culture in locales)
        {
            if (culture.Name == targetName)
            {
                return culture;
            }
        }

        foreach (CultureInfo culture in locales)
        {
            if (culture.Name == CultureInfo.CurrentCulture.Name)
            {
                return culture;
            }
        }

        return CultureInfo.CurrentCulture;
    }
}
