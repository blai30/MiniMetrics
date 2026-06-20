using System.Globalization;
using MiniMetrics.Lib;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Tests;

[TestClass]
public class SettingsViewModelTests
{
    private static Settings SampleSettings() => new()
    {
        BackgroundColor = "#0F121D",
        Opacity = 96,
        AlwaysOnTop = true,
        SnapToEdges = true,
        Visibility =
        {
            ["cpu.usage"] = true,
            ["cpu.temp"] = false,
            ["ram.usage"] = false,
            ["gpu.usage"] = true,
            ["gpu.temp"] = true,
            ["gpu.power"] = true,
            ["vram.usage"] = true
        }
    };

    // Records every change raised on the single SettingChanged channel after the view model is seeded.
    private static List<SettingChange> Capture(SettingsViewModel viewModel)
    {
        var changes = new List<SettingChange>();
        viewModel.SettingChanged += changes.Add;
        return changes;
    }

    [TestMethod]
    public void Seeds_values_from_settings()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.AreEqual("#0F121D", viewModel.BackgroundColor);
        Assert.AreEqual(96, viewModel.Opacity);
        Assert.IsTrue(viewModel.ToggleFor("cpu.usage").IsVisible);
        Assert.IsFalse(viewModel.ToggleFor("cpu.temp").IsVisible);
        Assert.IsFalse(viewModel.ToggleFor("ram.usage").IsVisible);
    }

    [TestMethod]
    public void Groups_metrics_by_card_with_uppercase_headers()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        CollectionAssert.AreEqual(new[] { "CPU", "RAM", "GPU", "VRAM" },
            viewModel.MetricGroups.Select(group => group.Header).ToArray());
        CollectionAssert.AreEqual(new[] { "Usage", "Temperature", "Power" },
            viewModel.MetricGroups[0].Toggles.Select(toggle => toggle.Label).ToArray());
    }

    [TestMethod]
    public void Seeds_per_metric_toggles_from_legacy_card_key()
    {
        var settings = new Settings { Visibility = { ["gpu"] = false } };

        var viewModel = new SettingsViewModel(settings);

        Assert.IsFalse(viewModel.ToggleFor("gpu.usage").IsVisible);
        Assert.IsFalse(viewModel.ToggleFor("gpu.temp").IsVisible);
        Assert.IsFalse(viewModel.ToggleFor("gpu.power").IsVisible);
    }

    [TestMethod]
    public void Changing_color_raises_an_appearance_change()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        var changes = Capture(viewModel);

        viewModel.BackgroundColor = "#1A1F2B";

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Appearance, changes[0].Kind);
    }

    [TestMethod]
    public void Changing_opacity_raises_an_appearance_change()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        var changes = Capture(viewModel);

        viewModel.Opacity = 50;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Appearance, changes[0].Kind);
    }

    [TestMethod]
    public void Toggling_metric_raises_a_metric_visibility_change_with_key_and_value()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        var changes = Capture(viewModel);

        viewModel.ToggleFor("ram.usage").IsVisible = true;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.MetricVisibility, changes[0].Kind);
        Assert.AreEqual("ram.usage", changes[0].Key);
        Assert.IsTrue(changes[0].Flag);
    }

    [TestMethod]
    public void Toggling_gpu_power_raises_a_metric_visibility_change_with_dotted_key()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        var changes = Capture(viewModel);

        viewModel.ToggleFor("gpu.power").IsVisible = false;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.MetricVisibility, changes[0].Kind);
        Assert.AreEqual("gpu.power", changes[0].Key);
        Assert.IsFalse(changes[0].Flag);
    }

    [TestMethod]
    public void Seeding_a_toggle_does_not_raise_a_change()
    {
        var settings = SampleSettings();

        var viewModel = new SettingsViewModel(settings);
        var changes = Capture(viewModel);

        // Construction already seeded the toggles with no subscriber; nothing fires now.
        Assert.AreEqual(0, changes.Count);
    }

    [TestMethod]
    public void SelectPreset_sets_background_color()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        viewModel.SelectPresetCommand.Execute("#18181B");

        Assert.AreEqual("#18181B", viewModel.BackgroundColor);
    }

    [TestMethod]
    public void System_theme_with_dark_os_edits_the_dark_color()
    {
        var settings = new Settings { BackgroundColor = "#0F121D", LightBackgroundColor = "#EEF1F5" };

        var viewModel = new SettingsViewModel(settings, true);

        Assert.AreEqual(AppTheme.System, viewModel.Theme);
        Assert.IsTrue(viewModel.EditingVariantIsDark);
        Assert.AreEqual("#0F121D", viewModel.BackgroundColor);
    }

    [TestMethod]
    public void Switching_to_light_loads_the_light_color_and_swatches()
    {
        var settings = new Settings { BackgroundColor = "#0F121D", LightBackgroundColor = "#EEF1F5" };
        var viewModel = new SettingsViewModel(settings, true);
        var changes = Capture(viewModel);

        viewModel.Theme = AppTheme.Light;

        Assert.IsTrue(changes.Any(change => change.Kind == SettingKind.Theme));
        Assert.IsFalse(viewModel.EditingVariantIsDark);
        Assert.AreEqual("#EEF1F5", viewModel.BackgroundColor);
        Assert.AreEqual("#FFFFFF", viewModel.Swatches[2]);
    }

    [TestMethod]
    public void Editing_color_under_each_theme_keeps_both_independently()
    {
        var settings = new Settings { BackgroundColor = "#0F121D", LightBackgroundColor = "#EEF1F5" };
        var viewModel = new SettingsViewModel(settings, true);

        viewModel.BackgroundColor = "#101010"; // edits dark
        viewModel.Theme = AppTheme.Light;
        viewModel.BackgroundColor = "#FAFAFA"; // edits light
        viewModel.Theme = AppTheme.Dark;

        Assert.AreEqual("#101010", viewModel.BackgroundColor);
    }

    [TestMethod]
    public void Seeds_selected_time_zone_from_settings_id()
    {
        var settings = new Settings { TimeZoneId = "UTC" };

        var viewModel = new SettingsViewModel(settings);

        Assert.AreEqual("UTC", viewModel.SelectedTimeZone.Id);
    }

    [TestMethod]
    public void Defaults_selected_time_zone_to_local_when_id_absent()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.AreEqual(TimeZoneInfo.Local.Id, viewModel.SelectedTimeZone.Id);
    }

    [TestMethod]
    public void Changing_time_zone_raises_a_time_zone_change()
    {
        var viewModel = new SettingsViewModel(new() { TimeZoneId = "UTC" });
        var changes = Capture(viewModel);

        var target = TimeZoneInfo.GetSystemTimeZones().First(tz => tz.Id != viewModel.SelectedTimeZone.Id);
        viewModel.SelectedTimeZone = target;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.TimeZone, changes[0].Kind);
    }

    [TestMethod]
    public void Use_local_time_defaults_true_when_id_absent()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.IsTrue(viewModel.UseLocalTime);
    }

    [TestMethod]
    public void Use_local_time_defaults_false_when_id_present()
    {
        var viewModel = new SettingsViewModel(new() { TimeZoneId = "UTC" });

        Assert.IsFalse(viewModel.UseLocalTime);
    }

    [TestMethod]
    public void Toggling_use_local_time_raises_a_time_zone_change()
    {
        var viewModel = new SettingsViewModel(new() { TimeZoneId = "UTC" });
        var changes = Capture(viewModel);

        viewModel.UseLocalTime = true;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.TimeZone, changes[0].Kind);
    }

    [TestMethod]
    public void Seeds_update_preferences_from_settings()
    {
        var settings = new Settings { UpdateCheckEnabled = false, UpdateFrequency = UpdateCheckFrequency.Weekly };

        var viewModel = new SettingsViewModel(settings);

        Assert.IsFalse(viewModel.UpdateCheckEnabled);
        Assert.AreEqual(UpdateCheckFrequency.Weekly, viewModel.UpdateFrequency);
        Assert.AreEqual(4, viewModel.UpdateFrequencies.Count);
    }

    [TestMethod]
    public void Changing_an_update_preference_raises_an_update_preferences_change()
    {
        var viewModel = new SettingsViewModel(new());
        var changes = Capture(viewModel);

        viewModel.UpdateCheckEnabled = false;
        viewModel.UpdateFrequency = UpdateCheckFrequency.Monthly;

        Assert.AreEqual(2, changes.Count);
        Assert.IsTrue(changes.All(change => change.Kind == SettingKind.UpdatePreferences));
    }

    [TestMethod]
    public void Seeds_compact_flags_from_settings()
    {
        var settings = new Settings { CpuCompact = true, GpuCompact = false, DateTimeCompact = true };

        var viewModel = new SettingsViewModel(settings);

        Assert.IsTrue(viewModel.CpuCompact);
        Assert.IsFalse(viewModel.GpuCompact);
        Assert.IsTrue(viewModel.DateTimeCompact);
    }

    [TestMethod]
    public void Toggling_cpu_compact_raises_a_compact_change_with_widget_key()
    {
        var viewModel = new SettingsViewModel(new());
        var changes = Capture(viewModel);

        viewModel.CpuCompact = true;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Compact, changes[0].Kind);
        Assert.AreEqual("cpu", changes[0].Key);
        Assert.IsTrue(changes[0].Flag);
    }

    [TestMethod]
    public void Toggling_gpu_compact_raises_a_compact_change_with_widget_key()
    {
        var viewModel = new SettingsViewModel(new());
        var changes = Capture(viewModel);

        viewModel.GpuCompact = true;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Compact, changes[0].Kind);
        Assert.AreEqual("gpu", changes[0].Key);
        Assert.IsTrue(changes[0].Flag);
    }

    [TestMethod]
    public void Toggling_clock_compact_raises_a_compact_change_with_clock_key()
    {
        var viewModel = new SettingsViewModel(new());
        var changes = Capture(viewModel);

        viewModel.DateTimeCompact = true;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Compact, changes[0].Kind);
        Assert.AreEqual("clock", changes[0].Key);
        Assert.IsTrue(changes[0].Flag);
    }

    [TestMethod]
    public void Seeds_clock_alignment_from_settings()
    {
        var settings = new Settings { ClockAlignment = ClockAlignment.Right };

        var viewModel = new SettingsViewModel(settings);

        Assert.AreEqual(ClockAlignment.Right, viewModel.ClockAlignment);
    }

    [TestMethod]
    public void Changing_clock_alignment_raises_a_clock_alignment_change()
    {
        var viewModel = new SettingsViewModel(new());
        var changes = Capture(viewModel);

        viewModel.ClockAlignment = ClockAlignment.Center;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.ClockAlignment, changes[0].Kind);
        Assert.AreEqual(ClockAlignment.Center, changes[0].Alignment);
    }

    [TestMethod]
    public void Seeds_selected_locale_from_settings_id()
    {
        var viewModel = new SettingsViewModel(new() { ClockLocaleId = "fr-FR" });

        Assert.AreEqual("fr-FR", viewModel.SelectedLocale.Name);
    }

    [TestMethod]
    public void Defaults_selected_locale_to_an_entry_in_the_list()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        // The dropdown must always have a selectable entry. A specific machine culture is selected
        // directly; a neutral one falls back to a specific culture in the list.
        Assert.IsTrue(viewModel.Locales.Contains(viewModel.SelectedLocale));

        var current = CultureInfo.CurrentCulture;
        if (viewModel.Locales.Any(culture => culture.Name == current.Name))
            Assert.AreEqual(current.Name, viewModel.SelectedLocale.Name);
    }

    [TestMethod]
    public void Changing_a_clock_format_raises_a_clock_formats_change()
    {
        var viewModel = new SettingsViewModel(new());
        var changes = Capture(viewModel);

        viewModel.ClockDateFormat = "yyyy-MM-dd";

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.ClockFormats, changes[0].Kind);
    }

    [TestMethod]
    public void Changing_locale_raises_a_clock_locale_change()
    {
        var viewModel = new SettingsViewModel(new() { ClockLocaleId = "en-US" });
        var changes = Capture(viewModel);

        viewModel.SelectedLocale = viewModel.Locales.First(culture => culture.Name == "fr-FR");

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.ClockLocale, changes[0].Kind);
    }

    [TestMethod]
    public void Clearing_the_locale_to_null_is_ignored_and_does_not_raise_a_change()
    {
        // The locale AutoCompleteBox clears its selection to null while the user types a filter; that
        // must not raise the change event (the host handles it by dereferencing the locale).
        var viewModel = new SettingsViewModel(new() { ClockLocaleId = "en-US" });
        var changes = Capture(viewModel);

        viewModel.SelectedLocale = null!;

        Assert.AreEqual(0, changes.Count);
    }

    [TestMethod]
    public void Time_sample_reflects_a_valid_custom_format()
    {
        // TimeZoneId "UTC" makes the preview zone deterministic; en-US makes the rendering deterministic.
        var viewModel = new SettingsViewModel(new() { TimeZoneId = "UTC", ClockLocaleId = "en-US" });

        viewModel.ClockTimeFormat = "HH:mm";

        Assert.AreEqual("14:26", viewModel.TimeSample);
    }

    [TestMethod]
    public void Blank_time_sample_shows_the_default_long_time()
    {
        var viewModel = new SettingsViewModel(new() { TimeZoneId = "UTC", ClockLocaleId = "en-US" });

        Assert.AreEqual("2:26:42 PM", viewModel.TimeSample);
    }

    [TestMethod]
    public void Invalid_format_sets_an_error_and_a_valid_one_clears_it()
    {
        var viewModel = new SettingsViewModel(new() { ClockLocaleId = "en-US" });

        viewModel.ClockTimeFormat = "h"; // lone standard specifier, invalid
        Assert.AreNotEqual("", viewModel.TimeFormatError);

        viewModel.ClockTimeFormat = "HH:mm";
        Assert.AreEqual("", viewModel.TimeFormatError);
    }

    [TestMethod]
    public void Populates_available_fonts_from_the_catalog_with_inter_first()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog("Verdana", "Arial"));

        Assert.AreEqual("Inter", viewModel.AvailableFonts[0]);
        CollectionAssert.Contains(viewModel.AvailableFonts.ToArray(), "Arial");
    }

    [TestMethod]
    public void Defaults_font_family_to_inter_when_unset()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog());

        Assert.AreEqual("Inter", viewModel.WidgetFontFamily);
        Assert.AreEqual(100, viewModel.WidgetScale);
        Assert.AreEqual(WidgetFontWeight.Regular, viewModel.WidgetFontWeight);
    }

    [TestMethod]
    public void Seeds_font_settings_from_saved_values()
    {
        var settings = new Settings
        {
            WidgetFontFamily = "Cascadia Code", WidgetScale = 130, WidgetFontWeight = WidgetFontWeight.Bold
        };

        var viewModel = new SettingsViewModel(settings, true, new FakeFontCatalog("Cascadia Code"));

        Assert.AreEqual("Cascadia Code", viewModel.WidgetFontFamily);
        Assert.AreEqual(130, viewModel.WidgetScale);
        Assert.AreEqual(WidgetFontWeight.Bold, viewModel.WidgetFontWeight);
    }

    [TestMethod]
    public void Changing_font_family_raises_a_widget_style_change()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog("Arial"));
        var changes = Capture(viewModel);

        viewModel.WidgetFontFamily = "Arial";

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.WidgetStyle, changes[0].Kind);
    }

    [TestMethod]
    public void Changing_scale_raises_a_widget_style_change()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog());
        var changes = Capture(viewModel);

        viewModel.WidgetScale = 120;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.WidgetStyle, changes[0].Kind);
    }

    [TestMethod]
    public void Changing_font_weight_raises_a_widget_style_change()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog());
        var changes = Capture(viewModel);

        viewModel.WidgetFontWeight = WidgetFontWeight.Light;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.WidgetStyle, changes[0].Kind);
    }

    [TestMethod]
    public void Plain_scalar_options_start_unmodified_at_defaults()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog());

        Assert.IsFalse(viewModel.ThemeModified);
        Assert.IsFalse(viewModel.OpacityModified);
        Assert.IsFalse(viewModel.WidgetScaleModified);
        Assert.IsFalse(viewModel.WidgetFontWeightModified);
        Assert.IsFalse(viewModel.ClockAlignmentModified);
        Assert.IsFalse(viewModel.UpdateFrequencyModified);
    }

    [TestMethod]
    public void Changing_a_plain_scalar_sets_its_modified_flag()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog());

        viewModel.Opacity = 50;
        viewModel.UpdateFrequency = UpdateCheckFrequency.Monthly;
        viewModel.ClockAlignment = ClockAlignment.Center;

        Assert.IsTrue(viewModel.OpacityModified);
        Assert.IsTrue(viewModel.UpdateFrequencyModified);
        Assert.IsTrue(viewModel.ClockAlignmentModified);
        Assert.IsFalse(viewModel.ThemeModified);
    }

    [TestMethod]
    public void Restoring_a_plain_scalar_resets_value_clears_flag_and_raises_a_change()
    {
        var settings = new Settings { Opacity = 50, UpdateFrequency = UpdateCheckFrequency.Monthly };
        var viewModel = new SettingsViewModel(settings, true, new FakeFontCatalog());
        var changes = Capture(viewModel);

        Assert.IsTrue(viewModel.OpacityModified);
        viewModel.RestoreOpacityCommand.Execute(null);
        Assert.AreEqual(96, viewModel.Opacity);
        Assert.IsFalse(viewModel.OpacityModified);

        viewModel.RestoreUpdateFrequencyCommand.Execute(null);
        Assert.AreEqual(UpdateCheckFrequency.Daily, viewModel.UpdateFrequency);
        Assert.IsFalse(viewModel.UpdateFrequencyModified);

        // restore routes through the normal change pipeline
        Assert.IsTrue(changes.Any(change => change.Kind == SettingKind.Appearance));
        Assert.IsTrue(changes.Any(change => change.Kind == SettingKind.UpdatePreferences));
    }

    [TestMethod]
    public void Editing_a_scalar_back_to_default_clears_the_modified_flag()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog());

        viewModel.WidgetScale = 130;
        Assert.IsTrue(viewModel.WidgetScaleModified);

        viewModel.WidgetScale = 100;
        Assert.IsFalse(viewModel.WidgetScaleModified);
    }

    [TestMethod]
    public void Background_color_unmodified_at_default_for_each_variant()
    {
        var settings = new Settings { BackgroundColor = "#0F121D", LightBackgroundColor = "#EEF1F5" };

        var darkVm = new SettingsViewModel(settings, true);
        Assert.IsFalse(darkVm.BackgroundColorModified);

        var lightVm = new SettingsViewModel(settings, false);
        Assert.IsFalse(lightVm.BackgroundColorModified);
    }

    [TestMethod]
    public void Background_color_modified_compares_against_the_current_variant_default()
    {
        var settings = new Settings { BackgroundColor = "#101010", LightBackgroundColor = "#EEF1F5" };

        var darkVm = new SettingsViewModel(settings, true);
        Assert.IsTrue(darkVm.BackgroundColorModified);

        // Light variant default is untouched, so under light the flag is clear.
        var lightVm = new SettingsViewModel(settings, false);
        Assert.IsFalse(lightVm.BackgroundColorModified);
    }

    [TestMethod]
    public void Restoring_background_color_resets_the_current_variant_only()
    {
        var settings = new Settings { BackgroundColor = "#101010", LightBackgroundColor = "#EEF1F5" };
        var viewModel = new SettingsViewModel(settings, true);
        var changes = Capture(viewModel);

        viewModel.RestoreBackgroundColorCommand.Execute(null);

        Assert.AreEqual("#0F121D", viewModel.BackgroundColor);
        Assert.IsFalse(viewModel.BackgroundColorModified);
        Assert.IsTrue(changes.Any(change => change.Kind == SettingKind.Appearance));
    }

    [TestMethod]
    public void Font_family_unmodified_at_default()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog("Arial"));

        Assert.IsFalse(viewModel.WidgetFontFamilyModified);
    }

    [TestMethod]
    public void Restoring_font_family_resets_to_inter_and_clears_flag()
    {
        var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog("Arial"));

        viewModel.WidgetFontFamily = "Arial";
        Assert.IsTrue(viewModel.WidgetFontFamilyModified);

        viewModel.RestoreWidgetFontFamilyCommand.Execute(null);
        Assert.AreEqual("Inter", viewModel.WidgetFontFamily);
        Assert.IsFalse(viewModel.WidgetFontFamilyModified);
    }

    [TestMethod]
    public void Time_zone_and_locale_unmodified_at_default()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.IsFalse(viewModel.TimeZoneModified);
        Assert.IsFalse(viewModel.LocaleModified);
    }

    [TestMethod]
    public void Time_zone_modified_when_not_local_and_restore_returns_to_local()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        var changes = Capture(viewModel);

        var other = viewModel.TimeZones.First(zone => zone.Id != viewModel.SelectedTimeZone.Id);
        viewModel.SelectedTimeZone = other;
        Assert.IsTrue(viewModel.TimeZoneModified);

        viewModel.RestoreTimeZoneCommand.Execute(null);
        Assert.AreEqual(TimeZoneInfo.Local.Id, viewModel.SelectedTimeZone.Id);
        Assert.IsFalse(viewModel.TimeZoneModified);
        Assert.IsTrue(changes.Any(change => change.Kind == SettingKind.TimeZone));
    }

    [TestMethod]
    public void Locale_modified_when_changed_and_restore_returns_to_current_culture()
    {
        var viewModel = new SettingsViewModel(new() { ClockLocaleId = "en-US" });
        var changes = Capture(viewModel);

        viewModel.SelectedLocale = viewModel.Locales.First(culture => culture.Name == "fr-FR");
        Assert.IsTrue(viewModel.LocaleModified);

        viewModel.RestoreLocaleCommand.Execute(null);
        Assert.IsFalse(viewModel.LocaleModified);
        Assert.IsTrue(changes.Any(change => change.Kind == SettingKind.ClockLocale));
    }

    [TestMethod]
    public void Transient_null_selection_does_not_report_modified()
    {
        var viewModel = new SettingsViewModel(new() { ClockLocaleId = "en-US" });

        // The AutoCompleteBox clears its selection to null while the user types; the flag getter must not throw.
        viewModel.SelectedLocale = null!;
        Assert.IsFalse(viewModel.LocaleModified);
    }

    [TestMethod]
    public void Clock_formats_unmodified_when_null_or_blank()
    {
        var viewModel = new SettingsViewModel(new());
        Assert.IsFalse(viewModel.ClockTimeFormatModified);

        viewModel.ClockTimeFormat = "   ";
        Assert.IsFalse(viewModel.ClockTimeFormatModified);
    }

    [TestMethod]
    public void Setting_a_clock_format_marks_it_modified()
    {
        var viewModel = new SettingsViewModel(new());

        viewModel.ClockDateFormat = "yyyy-MM-dd";
        Assert.IsTrue(viewModel.ClockDateFormatModified);
    }

    [TestMethod]
    public void Restoring_a_clock_format_writes_null_clears_flag_and_raises_a_change()
    {
        var viewModel = new SettingsViewModel(new() { ClockDateFormat = "yyyy-MM-dd" });
        var changes = Capture(viewModel);

        Assert.IsTrue(viewModel.ClockDateFormatModified);
        viewModel.RestoreClockDateFormatCommand.Execute(null);

        Assert.IsNull(viewModel.ClockDateFormat);
        Assert.IsFalse(viewModel.ClockDateFormatModified);
        Assert.IsTrue(changes.Any(change => change.Kind == SettingKind.ClockFormats));
    }
}
