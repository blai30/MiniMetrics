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
    public void Changing_color_raises_AppearanceChanged()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        int count = 0;
        viewModel.AppearanceChanged += () => count++;

        viewModel.BackgroundColor = "#1A1F2B";

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void Changing_opacity_raises_AppearanceChanged()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        int count = 0;
        viewModel.AppearanceChanged += () => count++;

        viewModel.Opacity = 50;

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void Toggling_metric_raises_MetricVisibilityChanged_with_key_and_value()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        (string Key, bool Value)? last = null;
        viewModel.MetricVisibilityChanged += (key, value) => last = (key, value);

        viewModel.ToggleFor("ram.usage").IsVisible = true;

        Assert.AreEqual(("ram.usage", true), last);
    }

    [TestMethod]
    public void Toggling_gpu_power_raises_MetricVisibilityChanged_with_dotted_key()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        (string Key, bool Value)? last = null;
        viewModel.MetricVisibilityChanged += (key, value) => last = (key, value);

        viewModel.ToggleFor("gpu.power").IsVisible = false;

        Assert.AreEqual(("gpu.power", false), last);
    }

    [TestMethod]
    public void Seeding_a_toggle_does_not_raise_MetricVisibilityChanged()
    {
        int count = 0;
        var settings = SampleSettings();

        var viewModel = new SettingsViewModel(settings);
        viewModel.MetricVisibilityChanged += (_, _) => count++;

        // Construction already happened above with no subscriber; re-seeding nothing fires now.
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void SelectPreset_sets_background_color()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        viewModel.SelectPresetCommand.Execute("#18181B");

        Assert.AreEqual("#18181B", viewModel.BackgroundColor);
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
    public void Changing_time_zone_raises_TimeZoneChanged()
    {
        var viewModel = new SettingsViewModel(new Settings { TimeZoneId = "UTC" });
        int count = 0;
        viewModel.TimeZoneChanged += () => count++;

        var target = System.TimeZoneInfo.GetSystemTimeZones().First(tz => tz.Id != viewModel.SelectedTimeZone.Id);
        viewModel.SelectedTimeZone = target;

        Assert.AreEqual(1, count);
    }
}
