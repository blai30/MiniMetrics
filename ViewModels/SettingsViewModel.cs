using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniMetrics.Lib;
using MiniMetrics.Models;
using MiniMetrics.Services;

namespace MiniMetrics.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    // Canonical defaults for the per-option restore affordance. A fresh Settings carries every
    // field-initializer default; the computed defaults (font, time zone, locale) are resolved in the
    // constructor below.
    private static readonly Settings Defaults = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColorModified))]
    public partial string BackgroundColor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpacityModified))]
    public partial int Opacity { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeModified))]
    [NotifyPropertyChangedFor(nameof(BackgroundColorModified))]
    public partial AppTheme Theme { get; set; }
    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    private static readonly string[] DarkSwatches =
        ["#0F121D", "#1A1F2B", "#18181B", "#0C1A2B", "#1E1726", "#11231C"];

    private static readonly string[] LightSwatches =
        ["#EEF1F5", "#E4E8EF", "#FFFFFF", "#EAF1F8", "#F3EEF7", "#EAF3EE"];

    private string _darkColor = "";
    private string _lightColor = "";

    // Which variant the color editor currently writes to. Under System it follows the OS as captured
    // when the window opened.
    public bool EditingVariantIsDark => Theme switch
    {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => field
    };

    public IReadOnlyList<string> Swatches => EditingVariantIsDark ? DarkSwatches : LightSwatches;

    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();

    [ObservableProperty] public partial TimeZoneInfo SelectedTimeZone { get; set; }
    [ObservableProperty] public partial bool UpdateCheckEnabled { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateFrequencyModified))]
    public partial UpdateCheckFrequency UpdateFrequency { get; set; }

    [ObservableProperty] public partial bool CpuCompact { get; set; }
    [ObservableProperty] public partial bool GpuCompact { get; set; }
    [ObservableProperty] public partial bool DateTimeCompact { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClockAlignmentModified))]
    public partial ClockAlignment ClockAlignment { get; set; }

    public IReadOnlyList<string> AvailableFonts { get; }

    public IReadOnlyList<WidgetFontWeight> WidgetFontWeights { get; } =
        [WidgetFontWeight.Light, WidgetFontWeight.Regular, WidgetFontWeight.Bold];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetFontFamilyModified))]
    public partial string WidgetFontFamily { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetScaleModified))]
    public partial int WidgetScale { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetFontWeightModified))]
    public partial WidgetFontWeight WidgetFontWeight { get; set; }
    [ObservableProperty] public partial bool UseLocalTime { get; set; }

    // The full set of specific cultures for the locale picker, ordered by display name.
    public IReadOnlyList<CultureInfo> Locales { get; } =
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .OrderBy(culture => culture.DisplayName, StringComparer.CurrentCulture)
            .ToArray();

    [ObservableProperty] public partial CultureInfo SelectedLocale { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeSample))]
    [NotifyPropertyChangedFor(nameof(TimeFormatError))]
    public partial string? ClockTimeFormat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateSample))]
    [NotifyPropertyChangedFor(nameof(DateFormatError))]
    public partial string? ClockDateFormat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeHoverSample))]
    [NotifyPropertyChangedFor(nameof(TimeHoverFormatError))]
    public partial string? ClockTimeFormatHover { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateHoverSample))]
    [NotifyPropertyChangedFor(nameof(DateHoverFormatError))]
    public partial string? ClockDateFormatHover { get; set; }

    // A fixed instant so the settings preview stays stable while the user edits.
    private static readonly DateTimeOffset PreviewInstant = new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero);

    // The zone used for previews mirrors the clock's resolved zone selection. SelectedTimeZone can be
    // momentarily null while the user types in the time zone search box, so fall back to local then.
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

    public IReadOnlyList<UpdateCheckFrequency> UpdateFrequencies { get; } =
    [
        UpdateCheckFrequency.EveryLaunch,
        UpdateCheckFrequency.Daily,
        UpdateCheckFrequency.Weekly,
        UpdateCheckFrequency.Monthly
    ];

    // The metric visibility checkboxes, grouped by card, built from the registry.
    public IReadOnlyList<MetricGroupViewModel> MetricGroups { get; }

    private readonly Dictionary<string, MetricToggleViewModel> _togglesByKey = new();

    public SettingsViewModel(Settings settings, bool systemIsDark = true, IFontCatalog? fonts = null)
    {
        EditingVariantIsDark = systemIsDark;
        Theme = settings.Theme;
        _darkColor = settings.BackgroundColor;
        _lightColor = settings.LightBackgroundColor;
        BackgroundColor = EditingVariantIsDark ? _darkColor : _lightColor;
        Opacity = settings.Opacity;
        UpdateCheckEnabled = settings.UpdateCheckEnabled;
        UpdateFrequency = settings.UpdateFrequency;
        CpuCompact = settings.CpuCompact;
        GpuCompact = settings.GpuCompact;
        DateTimeCompact = settings.DateTimeCompact;
        AvailableFonts = fonts?.AvailableFamilies() ?? [WidgetStyleProfile.DefaultFamilyName];
        WidgetFontFamily = settings.WidgetFontFamily ?? WidgetStyleProfile.DefaultFamilyName;
        WidgetScale = settings.WidgetScale;
        WidgetFontWeight = settings.WidgetFontWeight;
        ClockAlignment = settings.ClockAlignment;
        UseLocalTime = settings.TimeZoneId is null;
        ClockTimeFormat = settings.ClockTimeFormat;
        ClockDateFormat = settings.ClockDateFormat;
        ClockTimeFormatHover = settings.ClockTimeFormatHover;
        ClockDateFormatHover = settings.ClockDateFormatHover;

        var groups = new List<MetricGroupViewModel>();
        foreach (string card in MetricRegistry.Cards)
        {
            var toggles = new List<MetricToggleViewModel>();
            foreach (var entry in MetricRegistry.ForCard(card))
            {
                var toggle = new MetricToggleViewModel(
                    entry.Key,
                    entry.Label,
                    Seed(entry.Key, card),
                    (key, value) => SettingChanged?.Invoke(SettingChange.Metric(key, value)));
                toggles.Add(toggle);
                _togglesByKey[entry.Key] = toggle;
            }

            groups.Add(new(card.ToUpperInvariant(), toggles));
        }

        MetricGroups = groups;
        SelectedTimeZone = ResolveZone(settings.TimeZoneId, TimeZones);
        SelectedLocale = ResolveLocale(settings.ClockLocaleId, Locales);
        return;

        // Seed each per-metric toggle, falling back to the legacy whole-card key when the granular
        // one has not been saved yet, so an existing hidden card stays hidden after upgrading.
        bool Seed(string key, string legacy) =>
            settings.Visibility.TryGetValue(key, out bool value)
                ? value
                : settings.Visibility.GetValueOrDefault(legacy, true);
    }

    // Raised whenever any setting changes, carrying which facet changed and, for the per-key facets
    // (metric visibility, compact toggles, clock alignment), its payload. One channel so the host routes
    // on a single value instead of subscribing to a separate event per setting.
    public event Action<SettingChange>? SettingChanged;

    // The toggle for a metric key.
    public MetricToggleViewModel ToggleFor(string key) => _togglesByKey[key];

    // Each option's value differs from its default. The Settings-window reset button binds its
    // IsVisible to these so it surfaces only while the option strays from default.
    public bool ThemeModified => Theme != Defaults.Theme;
    public bool OpacityModified => Opacity != Defaults.Opacity;
    public bool WidgetScaleModified => WidgetScale != Defaults.WidgetScale;
    public bool WidgetFontFamilyModified => WidgetFontFamily != WidgetStyleProfile.DefaultFamilyName;
    public bool WidgetFontWeightModified => WidgetFontWeight != Defaults.WidgetFontWeight;
    public bool ClockAlignmentModified => ClockAlignment != Defaults.ClockAlignment;
    public bool UpdateFrequencyModified => UpdateFrequency != Defaults.UpdateFrequency;

    // The editor writes one theme variant at a time, so compare against and restore that variant's default.
    public bool BackgroundColorModified =>
        BackgroundColor != (EditingVariantIsDark ? Defaults.BackgroundColor : Defaults.LightBackgroundColor);

    [RelayCommand] private void RestoreTheme() => Theme = Defaults.Theme;
    [RelayCommand] private void RestoreOpacity() => Opacity = Defaults.Opacity;
    [RelayCommand] private void RestoreWidgetScale() => WidgetScale = Defaults.WidgetScale;
    [RelayCommand] private void RestoreWidgetFontFamily() => WidgetFontFamily = WidgetStyleProfile.DefaultFamilyName;
    [RelayCommand] private void RestoreWidgetFontWeight() => WidgetFontWeight = Defaults.WidgetFontWeight;
    [RelayCommand] private void RestoreClockAlignment() => ClockAlignment = Defaults.ClockAlignment;
    [RelayCommand] private void RestoreUpdateFrequency() => UpdateFrequency = Defaults.UpdateFrequency;

    [RelayCommand]
    private void RestoreBackgroundColor() =>
        BackgroundColor = EditingVariantIsDark ? Defaults.BackgroundColor : Defaults.LightBackgroundColor;

    [RelayCommand]
    private void SelectPreset(string hex) => BackgroundColor = hex;

    partial void OnBackgroundColorChanged(string value)
    {
        if (EditingVariantIsDark)
            _darkColor = value;
        else
            _lightColor = value;

        SettingChanged?.Invoke(SettingChange.Of(SettingKind.Appearance));
    }

    partial void OnOpacityChanged(int value) => SettingChanged?.Invoke(SettingChange.Of(SettingKind.Appearance));

    partial void OnThemeChanged(AppTheme value)
    {
        // Apply the variant first so the host resolves the new theme, then load that theme's stored
        // color and swatch set into the editor.
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.Theme));
        BackgroundColor = EditingVariantIsDark ? _darkColor : _lightColor;
        OnPropertyChanged(nameof(Swatches));
    }

    partial void OnSelectedTimeZoneChanged(TimeZoneInfo value)
    {
        // The time zone search box clears its selection to null while the user types a filter; ignore
        // that so we never persist or dereference a null zone.
        if (value is null) return;

        SettingChanged?.Invoke(SettingChange.Of(SettingKind.TimeZone));
    }

    partial void OnUseLocalTimeChanged(bool value) => SettingChanged?.Invoke(SettingChange.Of(SettingKind.TimeZone));

    partial void OnClockTimeFormatChanged(string? value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.ClockFormats));

    partial void OnClockDateFormatChanged(string? value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.ClockFormats));

    partial void OnClockTimeFormatHoverChanged(string? value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.ClockFormats));

    partial void OnClockDateFormatHoverChanged(string? value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.ClockFormats));

    partial void OnSelectedLocaleChanged(CultureInfo value)
    {
        // The locale box clears its selection to null while the user types a filter; ignore that so we
        // never persist a null locale or dereference it downstream.
        if (value is null) return;

        // The samples and errors all depend on the locale, so refresh every one, then notify the host.
        OnPropertyChanged(nameof(TimeSample));
        OnPropertyChanged(nameof(DateSample));
        OnPropertyChanged(nameof(TimeHoverSample));
        OnPropertyChanged(nameof(DateHoverSample));
        OnPropertyChanged(nameof(TimeFormatError));
        OnPropertyChanged(nameof(DateFormatError));
        OnPropertyChanged(nameof(TimeHoverFormatError));
        OnPropertyChanged(nameof(DateHoverFormatError));
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.ClockLocale));
    }

    partial void OnUpdateCheckEnabledChanged(bool value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.UpdatePreferences));

    partial void OnUpdateFrequencyChanged(UpdateCheckFrequency value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.UpdatePreferences));

    partial void OnCpuCompactChanged(bool value) => SettingChanged?.Invoke(SettingChange.Compact("cpu", value));

    partial void OnGpuCompactChanged(bool value) => SettingChanged?.Invoke(SettingChange.Compact("gpu", value));

    partial void OnDateTimeCompactChanged(bool value) => SettingChanged?.Invoke(SettingChange.Compact("clock", value));

    partial void OnWidgetFontFamilyChanged(string value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.WidgetStyle));

    partial void OnWidgetScaleChanged(int value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.WidgetStyle));

    partial void OnWidgetFontWeightChanged(WidgetFontWeight value) =>
        SettingChanged?.Invoke(SettingChange.Of(SettingKind.WidgetStyle));

    partial void OnClockAlignmentChanged(ClockAlignment value) =>
        SettingChanged?.Invoke(SettingChange.ForAlignment(value));

    // Picks the saved zone by id, else the machine's local zone (matched from the list so the
    // dropdown highlights it), else local as a last resort.
    private static TimeZoneInfo ResolveZone(string? id, IReadOnlyList<TimeZoneInfo> zones)
    {
        string targetId = id ?? TimeZoneInfo.Local.Id;
        foreach (var zone in zones)
            if (zone.Id == targetId)
                return zone;

        foreach (var zone in zones)
            if (zone.Id == TimeZoneInfo.Local.Id)
                return zone;

        // Last resort if the system list somehow lacks the local zone; the dropdown shows no
        // selection in that case.
        return TimeZoneInfo.Local;
    }

    // Picks the saved culture by name, else the machine's current culture, matched from the list so
    // the dropdown highlights it. A neutral machine culture (for example "en" with no region) is not
    // in the specific-culture list, so fall back to a specific culture under that parent, then to the
    // first entry, so the dropdown always shows a selection.
    private static CultureInfo ResolveLocale(string? id, IReadOnlyList<CultureInfo> locales)
    {
        string targetName = id ?? CultureInfo.CurrentCulture.Name;
        foreach (var culture in locales)
            if (culture.Name == targetName)
                return culture;

        foreach (var culture in locales)
            if (culture.Name == CultureInfo.CurrentCulture.Name)
                return culture;

        foreach (var culture in locales)
            if (culture.Parent.Name == CultureInfo.CurrentCulture.Name)
                return culture;

        return locales.Count > 0 ? locales[0] : CultureInfo.CurrentCulture;
    }
}
