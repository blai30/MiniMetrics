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
    private bool _cpuPowerVisible;

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

    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();

    [ObservableProperty]
    private TimeZoneInfo _selectedTimeZone;

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
        _cpuPowerVisible = Seed("cpu.power", "cpu");
        _ramVisible = Seed("ram.usage", "ram");
        _gpuUsageVisible = Seed("gpu.usage", "gpu");
        _gpuTempVisible = Seed("gpu.temp", "gpu");
        _gpuPowerVisible = Seed("gpu.power", "gpu");
        _vramVisible = Seed("vram.usage", "vram");
        _selectedTimeZone = ResolveZone(settings.TimeZoneId, TimeZones);
    }

    // Raised when the base color or opacity changes (live preview + persist).
    public event Action? AppearanceChanged;

    // Raised when a single metric toggle changes, with its key and new value.
    public event Action<string, bool>? MetricVisibilityChanged;

    // Raised when the chosen time zone changes (persist + live clock update).
    public event Action? TimeZoneChanged;

    [RelayCommand]
    private void SelectPreset(string hex) => BackgroundColor = hex;

    partial void OnBackgroundColorChanged(string value) => AppearanceChanged?.Invoke();

    partial void OnOpacityChanged(int value) => AppearanceChanged?.Invoke();

    partial void OnCpuUsageVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("cpu.usage", value);

    partial void OnCpuTempVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("cpu.temp", value);

    partial void OnCpuPowerVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("cpu.power", value);

    partial void OnRamVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("ram.usage", value);

    partial void OnGpuUsageVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("gpu.usage", value);

    partial void OnGpuTempVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("gpu.temp", value);

    partial void OnGpuPowerVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("gpu.power", value);

    partial void OnVramVisibleChanged(bool value) => MetricVisibilityChanged?.Invoke("vram.usage", value);

    partial void OnSelectedTimeZoneChanged(TimeZoneInfo value) => TimeZoneChanged?.Invoke();

    // Picks the saved zone by id, else the machine's local zone (matched from the list so the
    // dropdown highlights it), else local as a last resort.
    private static TimeZoneInfo ResolveZone(string? id, IReadOnlyList<TimeZoneInfo> zones)
    {
        string targetId = id ?? TimeZoneInfo.Local.Id;
        foreach (TimeZoneInfo zone in zones)
        {
            if (zone.Id == targetId)
            {
                return zone;
            }
        }

        foreach (TimeZoneInfo zone in zones)
        {
            if (zone.Id == TimeZoneInfo.Local.Id)
            {
                return zone;
            }
        }

        // Last resort if the system list somehow lacks the local zone; the dropdown shows no
        // selection in that case.
        return TimeZoneInfo.Local;
    }
}
