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
        Visibility = { ["cpu"] = true, ["ram"] = false, ["gpu"] = true, ["vram"] = true },
    };

    [Fact]
    public void Seeds_values_from_settings()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        Assert.Equal("#0F121D", viewModel.BackgroundColor);
        Assert.Equal(96, viewModel.Opacity);
        Assert.True(viewModel.CpuVisible);
        Assert.False(viewModel.RamVisible);
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

        Assert.Equal(("ram", true), last);
    }

    [Fact]
    public void SelectPreset_sets_background_color()
    {
        var viewModel = new SettingsViewModel(SampleSettings());

        viewModel.SelectPresetCommand.Execute("#18181B");

        Assert.Equal("#18181B", viewModel.BackgroundColor);
    }
}
