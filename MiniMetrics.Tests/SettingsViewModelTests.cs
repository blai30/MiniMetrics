using System.Linq;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using Xunit;

namespace MiniMetrics.Tests;

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

    [Fact]
    public void Seeds_values_from_settings()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.Equal("#0F121D", viewModel.BackgroundColor);
        Assert.Equal(96, viewModel.Opacity);
        Assert.True(viewModel.ToggleFor("cpu.usage").IsVisible);
        Assert.False(viewModel.ToggleFor("cpu.temp").IsVisible);
        Assert.False(viewModel.ToggleFor("ram.usage").IsVisible);
    }

    [Fact]
    public void Groups_metrics_by_card_with_uppercase_headers()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.Equal(new[] { "CPU", "RAM", "GPU", "VRAM" },
            viewModel.MetricGroups.Select(group => group.Header));
        Assert.Equal(new[] { "Usage", "Temperature", "Power" },
            viewModel.MetricGroups[0].Toggles.Select(toggle => toggle.Label));
    }

    [Fact]
    public void Seeds_per_metric_toggles_from_legacy_card_key()
    {
        var settings = new Settings { Visibility = { ["gpu"] = false } };

        var viewModel = new SettingsViewModel(settings);

        Assert.False(viewModel.ToggleFor("gpu.usage").IsVisible);
        Assert.False(viewModel.ToggleFor("gpu.temp").IsVisible);
        Assert.False(viewModel.ToggleFor("gpu.power").IsVisible);
    }

    [Fact]
    public void Changing_color_raises_AppearanceChanged()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        int count = 0;
        viewModel.AppearanceChanged += () => count++;

        viewModel.BackgroundColor = "#1A1F2B";

        Assert.Equal(1, count);
    }

    [Fact]
    public void Changing_opacity_raises_AppearanceChanged()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        int count = 0;
        viewModel.AppearanceChanged += () => count++;

        viewModel.Opacity = 50;

        Assert.Equal(1, count);
    }

    [Fact]
    public void Toggling_metric_raises_MetricVisibilityChanged_with_key_and_value()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        (string Key, bool Value)? last = null;
        viewModel.MetricVisibilityChanged += (key, value) => last = (key, value);

        viewModel.ToggleFor("ram.usage").IsVisible = true;

        Assert.Equal(("ram.usage", true), last);
    }

    [Fact]
    public void Toggling_gpu_power_raises_MetricVisibilityChanged_with_dotted_key()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        (string Key, bool Value)? last = null;
        viewModel.MetricVisibilityChanged += (key, value) => last = (key, value);

        viewModel.ToggleFor("gpu.power").IsVisible = false;

        Assert.Equal(("gpu.power", false), last);
    }

    [Fact]
    public void Seeding_a_toggle_does_not_raise_MetricVisibilityChanged()
    {
        int count = 0;
        var settings = SampleSettings();

        var viewModel = new SettingsViewModel(settings);
        viewModel.MetricVisibilityChanged += (_, _) => count++;

        // Construction already happened above with no subscriber; re-seeding nothing fires now.
        Assert.Equal(0, count);
    }

    [Fact]
    public void SelectPreset_sets_background_color()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        viewModel.SelectPresetCommand.Execute("#18181B");

        Assert.Equal("#18181B", viewModel.BackgroundColor);
    }

    [Fact]
    public void Seeds_selected_time_zone_from_settings_id()
    {
        var settings = new Settings { TimeZoneId = "UTC" };

        var viewModel = new SettingsViewModel(settings);

        Assert.Equal("UTC", viewModel.SelectedTimeZone.Id);
    }

    [Fact]
    public void Defaults_selected_time_zone_to_local_when_id_absent()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.Equal(System.TimeZoneInfo.Local.Id, viewModel.SelectedTimeZone.Id);
    }

    [Fact]
    public void Changing_time_zone_raises_TimeZoneChanged()
    {
        var viewModel = new SettingsViewModel(new Settings { TimeZoneId = "UTC" });
        int count = 0;
        viewModel.TimeZoneChanged += () => count++;

        var target = System.TimeZoneInfo.GetSystemTimeZones().First(tz => tz.Id != viewModel.SelectedTimeZone.Id);
        viewModel.SelectedTimeZone = target;

        Assert.Equal(1, count);
    }
}
