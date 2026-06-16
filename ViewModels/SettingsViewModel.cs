using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniMetrics.Models;

namespace MiniMetrics.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _backgroundColor;

    [ObservableProperty]
    private int _opacity;

    [ObservableProperty]
    private bool _cpuUsageVisible;

    [ObservableProperty]
    private bool _cpuTempVisible;

    [ObservableProperty]
    private bool _ramVisible;

    [ObservableProperty]
    private bool _gpuUsageVisible;

    [ObservableProperty]
    private bool _gpuTempVisible;

    [ObservableProperty]
    private bool _gpuPowerVisible;

    [ObservableProperty]
    private bool _vramVisible;

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private bool _snapToEdges;

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
        _cpuUsageVisible = Seed("cpu.usage", "cpu");
        _cpuTempVisible = Seed("cpu.temp", "cpu");
        _ramVisible = Seed("ram.usage", "ram");
        _gpuUsageVisible = Seed("gpu.usage", "gpu");
        _gpuTempVisible = Seed("gpu.temp", "gpu");
        _gpuPowerVisible = Seed("gpu.power", "gpu");
        _vramVisible = Seed("vram.usage", "vram");
        _alwaysOnTop = settings.AlwaysOnTop;
        _snapToEdges = settings.SnapToEdges;
    }

    // Raised when the base color or opacity changes (live preview + persist).
    public event Action? AppearanceChanged;

    // Raised when a single metric toggle changes, with its key and new value.
    public event Action<string, bool>? MetricVisibilityChanged;

    // Raised when the always-on-top toggle changes, with its new value.
    public event Action<bool>? AlwaysOnTopChanged;

    // Raised when the snap-to-edges toggle changes, with its new value.
    public event Action<bool>? SnapToEdgesChanged;

    [RelayCommand]
    private void SelectPreset(string hex) => BackgroundColor = hex;

    partial void OnBackgroundColorChanged(string value) => AppearanceChanged?.Invoke();

    partial void OnOpacityChanged(int value) => AppearanceChanged?.Invoke();

    partial void OnCpuUsageVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("cpu.usage", value);

    partial void OnCpuTempVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("cpu.temp", value);

    partial void OnRamVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("ram.usage", value);

    partial void OnGpuUsageVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("gpu.usage", value);

    partial void OnGpuTempVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("gpu.temp", value);

    partial void OnGpuPowerVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("gpu.power", value);

    partial void OnVramVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("vram.usage", value);

    partial void OnAlwaysOnTopChanged(bool value) => AlwaysOnTopChanged?.Invoke(value);

    partial void OnSnapToEdgesChanged(bool value) => SnapToEdgesChanged?.Invoke(value);
}
