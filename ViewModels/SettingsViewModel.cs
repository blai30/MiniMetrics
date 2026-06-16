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
    private bool _cpuVisible;

    [ObservableProperty]
    private bool _ramVisible;

    [ObservableProperty]
    private bool _gpuVisible;

    [ObservableProperty]
    private bool _vramVisible;

    [ObservableProperty]
    private bool _alwaysOnTop;

    public SettingsViewModel(Settings settings)
    {
        _backgroundColor = settings.BackgroundColor;
        _opacity = settings.Opacity;
        _cpuVisible = settings.Visibility.GetValueOrDefault("cpu", true);
        _ramVisible = settings.Visibility.GetValueOrDefault("ram", true);
        _gpuVisible = settings.Visibility.GetValueOrDefault("gpu", true);
        _vramVisible = settings.Visibility.GetValueOrDefault("vram", true);
        _alwaysOnTop = settings.AlwaysOnTop;
    }

    // Raised when the base color or opacity changes (live preview + persist).
    public event Action? AppearanceChanged;

    // Raised when a single metric toggle changes, with its key and new value.
    public event Action<string, bool>? MetricVisibilityChanged;

    // Raised when the always-on-top toggle changes, with its new value.
    public event Action<bool>? AlwaysOnTopChanged;

    [RelayCommand]
    private void SelectPreset(string hex) => BackgroundColor = hex;

    partial void OnBackgroundColorChanged(string value) => AppearanceChanged?.Invoke();

    partial void OnOpacityChanged(int value) => AppearanceChanged?.Invoke();

    partial void OnCpuVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("cpu", value);

    partial void OnRamVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("ram", value);

    partial void OnGpuVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("gpu", value);

    partial void OnVramVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("vram", value);

    partial void OnAlwaysOnTopChanged(bool value) => AlwaysOnTopChanged?.Invoke(value);
}
