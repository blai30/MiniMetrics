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

    private readonly TimeZoneInfo _defaultTimeZone;
    private readonly CultureInfo _defaultLocale;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeZoneModified))]
    public partial TimeZoneInfo SelectedTimeZone { get; set; }

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

    public IReadOnlyList<ClockAlignment> ClockAlignments { get; } =
        [ClockAlignment.Left, ClockAlignment.Center, ClockAlignment.Right];

    public IReadOnlyList<string> AvailableFonts { get; }

    public IReadOnlyList<WidgetFontWeight> WidgetFontWeights { get; } =
        [WidgetFontWeight.Light, WidgetFontWeight.Regular, WidgetFontWeight.Bold];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetFontFamilyModified))]
    public partial string WidgetFontFamily { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuScaleModified))]
    public partial int CpuScale { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GpuScaleModified))]
    public partial int GpuScale { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClockScaleModified))]
    public partial int ClockScale { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetFontWeightModified))]
    public partial WidgetFontWeight WidgetFontWeight { get; set; }

    [ObservableProperty] public partial bool UseLocalTime { get; set; }

    // The full set of specific cultures for the locale picker, ordered by display name.
    public IReadOnlyList<CultureInfo> Locales { get; } =
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .OrderBy(culture => culture.DisplayName, StringComparer.CurrentCulture)
            .ToArray();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocaleModified))]
    public partial CultureInfo SelectedLocale { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeSample))]
    [NotifyPropertyChangedFor(nameof(TimeFormatError))]
    [NotifyPropertyChangedFor(nameof(ClockTimeFormatModified))]
    public partial string? ClockTimeFormat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateSample))]
    [NotifyPropertyChangedFor(nameof(DateFormatError))]
    [NotifyPropertyChangedFor(nameof(ClockDateFormatModified))]
    public partial string? ClockDateFormat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeHoverSample))]
    [NotifyPropertyChangedFor(nameof(TimeHoverFormatError))]
    [NotifyPropertyChangedFor(nameof(ClockTimeFormatHoverModified))]
    public partial string? ClockTimeFormatHover { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateHoverSample))]
    [NotifyPropertyChangedFor(nameof(DateHoverFormatError))]
    [NotifyPropertyChangedFor(nameof(ClockDateFormatHoverModified))]
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
        CpuScale = settings.WidgetScales.GetValueOrDefault("cpu", 100);
        GpuScale = settings.WidgetScales.GetValueOrDefault("gpu", 100);
        ClockScale = settings.WidgetScales.GetValueOrDefault("clock", 100);
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
                    entry.RequiresElevation,
                    Seed(entry.Key, card),
                    (key, value) => SettingChanged?.Invoke(new SettingChange.MetricVisibility(key, value)));
                toggles.Add(toggle);
                _togglesByKey[entry.Key] = toggle;
            }

            groups.Add(new(card.ToUpperInvariant(), toggles));
        }

        MetricGroups = groups;
        _defaultTimeZone = ResolveZone(null, TimeZones);
        SelectedTimeZone = ResolveZone(settings.TimeZoneId, TimeZones);
        _defaultLocale = ResolveLocale(null, Locales);
        SelectedLocale = ResolveLocale(settings.ClockLocaleId, Locales);
        return;

        // Seed each per-metric toggle, falling back to the legacy whole-card key when the granular
        // one has not been saved yet, so an existing hidden card stays hidden after upgrading.
        bool Seed(string key, string legacy) =>
            settings.Visibility.TryGetValue(key, out bool value)
                ? value
                : settings.Visibility.GetValueOrDefault(legacy, true);
    }

    // Raised whenever any setting changes, carrying the changed facet and its payload as one of the
    // SettingChange records. One channel so the host routes on a single value instead of subscribing to a
    // separate event per setting.
    public event Action<SettingChange>? SettingChanged;

    // The toggle for a metric key.
    public MetricToggleViewModel ToggleFor(string key) => _togglesByKey[key];

    // Each option's value differs from its default. The Settings-window reset button binds its
    // IsVisible to these so it surfaces only while the option strays from default.
    public bool ThemeModified => Theme != Defaults.Theme;
    public bool OpacityModified => Opacity != Defaults.Opacity;
    public bool CpuScaleModified => CpuScale != 100;
    public bool GpuScaleModified => GpuScale != 100;
    public bool ClockScaleModified => ClockScale != 100;
    public bool WidgetFontFamilyModified => WidgetFontFamily != WidgetStyleProfile.DefaultFamilyName;
    public bool WidgetFontWeightModified => WidgetFontWeight != Defaults.WidgetFontWeight;
    public bool ClockAlignmentModified => ClockAlignment != Defaults.ClockAlignment;
    public bool UpdateFrequencyModified => UpdateFrequency != Defaults.UpdateFrequency;

    // The editor writes one theme variant at a time, so compare against and restore that variant's default.
    public bool BackgroundColorModified =>
        BackgroundColor != (EditingVariantIsDark ? Defaults.BackgroundColor : Defaults.LightBackgroundColor);

    public bool TimeZoneModified => SelectedTimeZone is not null && SelectedTimeZone.Id != _defaultTimeZone.Id;
    public bool LocaleModified => SelectedLocale is not null && SelectedLocale.Name != _defaultLocale.Name;

    [RelayCommand]
    private void RestoreTheme() => Theme = Defaults.Theme;

    [RelayCommand]
    private void RestoreOpacity() => Opacity = Defaults.Opacity;

    [RelayCommand]
    private void RestoreCpuScale() => CpuScale = 100;

    [RelayCommand]
    private void RestoreGpuScale() => GpuScale = 100;

    [RelayCommand]
    private void RestoreClockScale() => ClockScale = 100;

    [RelayCommand]
    private void RestoreWidgetFontFamily() => WidgetFontFamily = WidgetStyleProfile.DefaultFamilyName;

    [RelayCommand]
    private void RestoreWidgetFontWeight() => WidgetFontWeight = Defaults.WidgetFontWeight;

    [RelayCommand]
    private void RestoreClockAlignment() => ClockAlignment = Defaults.ClockAlignment;

    [RelayCommand]
    private void RestoreUpdateFrequency() => UpdateFrequency = Defaults.UpdateFrequency;

    [RelayCommand]
    private void RestoreBackgroundColor() =>
        BackgroundColor = EditingVariantIsDark ? Defaults.BackgroundColor : Defaults.LightBackgroundColor;

    [RelayCommand]
    private void RestoreTimeZone() => SelectedTimeZone = _defaultTimeZone;

    [RelayCommand]
    private void RestoreLocale() => SelectedLocale = _defaultLocale;

    // A null or blank format means "use the built-in default", so blank counts as default and restore clears it.
    public bool ClockTimeFormatModified => !string.IsNullOrWhiteSpace(ClockTimeFormat);
    public bool ClockDateFormatModified => !string.IsNullOrWhiteSpace(ClockDateFormat);
    public bool ClockTimeFormatHoverModified => !string.IsNullOrWhiteSpace(ClockTimeFormatHover);
    public bool ClockDateFormatHoverModified => !string.IsNullOrWhiteSpace(ClockDateFormatHover);

    [RelayCommand]
    private void RestoreClockTimeFormat() => ClockTimeFormat = null;

    [RelayCommand]
    private void RestoreClockDateFormat() => ClockDateFormat = null;

    [RelayCommand]
    private void RestoreClockTimeFormatHover() => ClockTimeFormatHover = null;

    [RelayCommand]
    private void RestoreClockDateFormatHover() => ClockDateFormatHover = null;

    [RelayCommand]
    private void SelectPreset(string hex) => BackgroundColor = hex;

    partial void OnBackgroundColorChanged(string value)
    {
        if (EditingVariantIsDark)
            _darkColor = value;
        else
            _lightColor = value;

        RaiseAppearance();
    }

    partial void OnOpacityChanged(int value) => RaiseAppearance();

    partial void OnThemeChanged(AppTheme value)
    {
        // Apply the variant first so the host resolves the new theme, then load that theme's stored
        // color and swatch set into the editor.
        SettingChanged?.Invoke(new SettingChange.Theme(value));
        BackgroundColor = EditingVariantIsDark ? _darkColor : _lightColor;
        OnPropertyChanged(nameof(Swatches));
    }

    partial void OnSelectedTimeZoneChanged(TimeZoneInfo value)
    {
        // The time zone search box clears its selection to null while the user types a filter; ignore
        // that so we never persist or dereference a null zone.
        if (value is null) return;

        RaiseTimeZone();
    }

    partial void OnUseLocalTimeChanged(bool value) => RaiseTimeZone();

    partial void OnClockTimeFormatChanged(string? value) =>
        RaiseClockFormats();

    partial void OnClockDateFormatChanged(string? value) =>
        RaiseClockFormats();

    partial void OnClockTimeFormatHoverChanged(string? value) =>
        RaiseClockFormats();

    partial void OnClockDateFormatHoverChanged(string? value) =>
        RaiseClockFormats();

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
        SettingChanged?.Invoke(new SettingChange.ClockLocale(value));
    }

    partial void OnUpdateCheckEnabledChanged(bool value) =>
        RaiseUpdatePreferences();

    partial void OnUpdateFrequencyChanged(UpdateCheckFrequency value) =>
        RaiseUpdatePreferences();

    partial void OnCpuCompactChanged(bool value) => SettingChanged?.Invoke(new SettingChange.Compact("cpu", value));

    partial void OnGpuCompactChanged(bool value) => SettingChanged?.Invoke(new SettingChange.Compact("gpu", value));

    partial void OnDateTimeCompactChanged(bool value) =>
        SettingChanged?.Invoke(new SettingChange.Compact("clock", value));

    partial void OnWidgetFontFamilyChanged(string value) =>
        RaiseWidgetStyle("cpu");

    partial void OnCpuScaleChanged(int value) =>
        RaiseWidgetStyle("cpu");

    partial void OnGpuScaleChanged(int value) =>
        RaiseWidgetStyle("gpu");

    partial void OnClockScaleChanged(int value) =>
        RaiseWidgetStyle("clock");

    partial void OnWidgetFontWeightChanged(WidgetFontWeight value) =>
        RaiseWidgetStyle("cpu");

    partial void OnClockAlignmentChanged(ClockAlignment value) =>
        SettingChanged?.Invoke(new SettingChange.Alignment(value));

    // The read-back facets bundle the view model's current values so the host never reaches back in.
    private void RaiseAppearance() =>
        SettingChanged?.Invoke(new SettingChange.Appearance(EditingVariantIsDark, BackgroundColor, Opacity));

    private void RaiseTimeZone() =>
        SettingChanged?.Invoke(new SettingChange.TimeZone(UseLocalTime ? null : SelectedTimeZone?.Id));

    private void RaiseClockFormats() =>
        SettingChanged?.Invoke(new SettingChange.ClockFormats(
            ClockTimeFormat, ClockDateFormat, ClockTimeFormatHover, ClockDateFormatHover));

    private void RaiseUpdatePreferences() =>
        SettingChanged?.Invoke(new SettingChange.UpdatePreferences(UpdateCheckEnabled, UpdateFrequency));

    private void RaiseWidgetStyle(string widget)
    {
        int scale = widget switch
        {
            "cpu" => CpuScale,
            "gpu" => GpuScale,
            "clock" => ClockScale,
            _ => 100
        };
        SettingChanged?.Invoke(new SettingChange.WidgetStyle(widget, WidgetFontFamily, scale, WidgetFontWeight));
    }

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
