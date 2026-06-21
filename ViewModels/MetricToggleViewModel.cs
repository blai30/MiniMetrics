using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniMetrics.ViewModels;

// One metric's visibility checkbox in the settings window. Reports its key and the new value when
// toggled; the seeded value is set through the field so seeding does not fire the change.
public sealed partial class MetricToggleViewModel(
    string key,
    string label,
    bool requiresElevation,
    bool isVisible,
    Action<string, bool> onChanged)
    : ObservableObject
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public bool RequiresElevation { get; } = requiresElevation;

    [ObservableProperty] public partial bool IsVisible { get; set; } = isVisible;

    partial void OnIsVisibleChanged(bool value) => onChanged(Key, value);
}

// A labeled group of metric toggles (one card's worth) for the settings list.
public sealed class MetricGroupViewModel(string header, IReadOnlyList<MetricToggleViewModel> toggles)
{
    public string Header { get; } = header;

    public IReadOnlyList<MetricToggleViewModel> Toggles { get; } = toggles;
}
