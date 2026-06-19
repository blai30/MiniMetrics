using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            ["vram.usage"] = true,
        },
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
        List<SettingChange> changes = Capture(viewModel);

        viewModel.BackgroundColor = "#1A1F2B";

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Appearance, changes[0].Kind);
    }

    [TestMethod]
    public void Changing_opacity_raises_an_appearance_change()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        List<SettingChange> changes = Capture(viewModel);

        viewModel.Opacity = 50;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Appearance, changes[0].Kind);
    }

    [TestMethod]
    public void Toggling_metric_raises_a_metric_visibility_change_with_key_and_value()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        List<SettingChange> changes = Capture(viewModel);

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
        List<SettingChange> changes = Capture(viewModel);

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
        List<SettingChange> changes = Capture(viewModel);

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

        var viewModel = new SettingsViewModel(settings, systemIsDark: true);

        Assert.AreEqual(AppTheme.System, viewModel.Theme);
        Assert.IsTrue(viewModel.EditingVariantIsDark);
        Assert.AreEqual("#0F121D", viewModel.BackgroundColor);
    }

    [TestMethod]
    public void Switching_to_light_loads_the_light_color_and_swatches()
    {
        var settings = new Settings { BackgroundColor = "#0F121D", LightBackgroundColor = "#EEF1F5" };
        var viewModel = new SettingsViewModel(settings, systemIsDark: true);
        List<SettingChange> changes = Capture(viewModel);

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
        var viewModel = new SettingsViewModel(settings, systemIsDark: true);

        viewModel.BackgroundColor = "#101010";   // edits dark
        viewModel.Theme = AppTheme.Light;
        viewModel.BackgroundColor = "#FAFAFA";    // edits light
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

        Assert.AreEqual(System.TimeZoneInfo.Local.Id, viewModel.SelectedTimeZone.Id);
    }

    [TestMethod]
    public void Changing_time_zone_raises_a_time_zone_change()
    {
        var viewModel = new SettingsViewModel(new Settings { TimeZoneId = "UTC" });
        List<SettingChange> changes = Capture(viewModel);

        var target = System.TimeZoneInfo.GetSystemTimeZones().First(tz => tz.Id != viewModel.SelectedTimeZone.Id);
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
        var viewModel = new SettingsViewModel(new Settings { TimeZoneId = "UTC" });

        Assert.IsFalse(viewModel.UseLocalTime);
    }

    [TestMethod]
    public void Toggling_use_local_time_raises_a_time_zone_change()
    {
        var viewModel = new SettingsViewModel(new Settings { TimeZoneId = "UTC" });
        List<SettingChange> changes = Capture(viewModel);

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
        var viewModel = new SettingsViewModel(new Settings());
        List<SettingChange> changes = Capture(viewModel);

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
        var viewModel = new SettingsViewModel(new Settings());
        List<SettingChange> changes = Capture(viewModel);

        viewModel.CpuCompact = true;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Compact, changes[0].Kind);
        Assert.AreEqual("cpu", changes[0].Key);
        Assert.IsTrue(changes[0].Flag);
    }

    [TestMethod]
    public void Toggling_gpu_compact_raises_a_compact_change_with_widget_key()
    {
        var viewModel = new SettingsViewModel(new Settings());
        List<SettingChange> changes = Capture(viewModel);

        viewModel.GpuCompact = true;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.Compact, changes[0].Kind);
        Assert.AreEqual("gpu", changes[0].Key);
        Assert.IsTrue(changes[0].Flag);
    }

    [TestMethod]
    public void Toggling_clock_compact_raises_a_compact_change_with_clock_key()
    {
        var viewModel = new SettingsViewModel(new Settings());
        List<SettingChange> changes = Capture(viewModel);

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
        var viewModel = new SettingsViewModel(new Settings());
        List<SettingChange> changes = Capture(viewModel);

        viewModel.ClockAlignment = ClockAlignment.Center;

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.ClockAlignment, changes[0].Kind);
        Assert.AreEqual(ClockAlignment.Center, changes[0].Alignment);
    }

    [TestMethod]
    public void Seeds_selected_locale_from_settings_id()
    {
        var viewModel = new SettingsViewModel(new Settings { ClockLocaleId = "fr-FR" });

        Assert.AreEqual("fr-FR", viewModel.SelectedLocale.Name);
    }

    [TestMethod]
    public void Defaults_selected_locale_to_an_entry_in_the_list()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        // The dropdown must always have a selectable entry. A specific machine culture is selected
        // directly; a neutral one falls back to a specific culture in the list.
        Assert.IsTrue(viewModel.Locales.Contains(viewModel.SelectedLocale));

        var current = System.Globalization.CultureInfo.CurrentCulture;
        if (viewModel.Locales.Any(culture => culture.Name == current.Name))
        {
            Assert.AreEqual(current.Name, viewModel.SelectedLocale.Name);
        }
    }

    [TestMethod]
    public void Changing_a_clock_format_raises_a_clock_formats_change()
    {
        var viewModel = new SettingsViewModel(new Settings());
        List<SettingChange> changes = Capture(viewModel);

        viewModel.ClockDateFormat = "yyyy-MM-dd";

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.ClockFormats, changes[0].Kind);
    }

    [TestMethod]
    public void Changing_locale_raises_a_clock_locale_change()
    {
        var viewModel = new SettingsViewModel(new Settings { ClockLocaleId = "en-US" });
        List<SettingChange> changes = Capture(viewModel);

        viewModel.SelectedLocale = viewModel.Locales.First(culture => culture.Name == "fr-FR");

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(SettingKind.ClockLocale, changes[0].Kind);
    }

    [TestMethod]
    public void Clearing_the_locale_to_null_is_ignored_and_does_not_raise_a_change()
    {
        // The locale AutoCompleteBox clears its selection to null while the user types a filter; that
        // must not raise the change event (the host handles it by dereferencing the locale).
        var viewModel = new SettingsViewModel(new Settings { ClockLocaleId = "en-US" });
        List<SettingChange> changes = Capture(viewModel);

        viewModel.SelectedLocale = null!;

        Assert.AreEqual(0, changes.Count);
    }

    [TestMethod]
    public void Time_sample_reflects_a_valid_custom_format()
    {
        // TimeZoneId "UTC" makes the preview zone deterministic; en-US makes the rendering deterministic.
        var viewModel = new SettingsViewModel(new Settings { TimeZoneId = "UTC", ClockLocaleId = "en-US" });

        viewModel.ClockTimeFormat = "HH:mm";

        Assert.AreEqual("14:26", viewModel.TimeSample);
    }

    [TestMethod]
    public void Blank_time_sample_shows_the_default_long_time()
    {
        var viewModel = new SettingsViewModel(new Settings { TimeZoneId = "UTC", ClockLocaleId = "en-US" });

        Assert.AreEqual("2:26:42 PM", viewModel.TimeSample);
    }

    [TestMethod]
    public void Invalid_format_sets_an_error_and_a_valid_one_clears_it()
    {
        var viewModel = new SettingsViewModel(new Settings { ClockLocaleId = "en-US" });

        viewModel.ClockTimeFormat = "h"; // lone standard specifier, invalid
        Assert.AreNotEqual("", viewModel.TimeFormatError);

        viewModel.ClockTimeFormat = "HH:mm";
        Assert.AreEqual("", viewModel.TimeFormatError);
    }
}
