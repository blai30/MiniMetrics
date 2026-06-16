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
        Assert.True(viewModel.CpuUsageVisible);
        Assert.False(viewModel.CpuTempVisible);
        Assert.False(viewModel.RamVisible);
        Assert.True(viewModel.AlwaysOnTop);
    }

    [Fact]
    public void Seeds_per_metric_toggles_from_legacy_card_key()
    {
        var settings = new Settings { Visibility = { ["gpu"] = false } };

        var viewModel = new SettingsViewModel(settings);

        Assert.False(viewModel.GpuUsageVisible);
        Assert.False(viewModel.GpuTempVisible);
        Assert.False(viewModel.GpuPowerVisible);
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

        viewModel.RamVisible = true;

        Assert.Equal(("ram.usage", true), last);
    }

    [Fact]
    public void Toggling_gpu_power_raises_MetricVisibilityChanged_with_dotted_key()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        (string Key, bool Value)? last = null;
        viewModel.MetricVisibilityChanged += (key, value) => last = (key, value);

        viewModel.GpuPowerVisible = false;

        Assert.Equal(("gpu.power", false), last);
    }

    [Fact]
    public void Toggling_always_on_top_raises_AlwaysOnTopChanged_with_value()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        bool? last = null;
        viewModel.AlwaysOnTopChanged += value => last = value;

        viewModel.AlwaysOnTop = false;

        Assert.Equal(false, last);
    }

    [Fact]
    public void Seeds_snap_to_edges_from_settings()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.True(viewModel.SnapToEdges);
    }

    [Fact]
    public void Toggling_snap_to_edges_raises_SnapToEdgesChanged_with_value()
    {
        var viewModel = new SettingsViewModel(SampleSettings());
        bool? last = null;
        viewModel.SnapToEdgesChanged += value => last = value;

        viewModel.SnapToEdges = false;

        Assert.Equal(false, last);
    }

    [Fact]
    public void SelectPreset_sets_background_color()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        viewModel.SelectPresetCommand.Execute("#18181B");

        Assert.Equal("#18181B", viewModel.BackgroundColor);
    }
}
