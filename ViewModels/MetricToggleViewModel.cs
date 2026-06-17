using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniMetrics.ViewModels;

// One metric's visibility checkbox in the settings window. Reports its key and the new value when
// toggled; the seeded value is set through the field so seeding does not fire the change.
public sealed partial class MetricToggleViewModel : ObservableObject
{
    private readonly Action<string, bool> _onChanged;

    public MetricToggleViewModel(string key, string label, bool isVisible, Action<string, bool> onChanged)
    {
        Key = key;
        Label = label;
        _isVisible = isVisible;
        _onChanged = onChanged;
    }

    public string Key { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isVisible;

    partial void OnIsVisibleChanged(bool value) => _onChanged(Key, value);
}

// A labeled group of metric toggles (one card's worth) for the settings list.
public sealed class MetricGroupViewModel
{
    public MetricGroupViewModel(string header, IReadOnlyList<MetricToggleViewModel> toggles)
    {
        Header = header;
        Toggles = toggles;
    }

    public string Header { get; }

    public IReadOnlyList<MetricToggleViewModel> Toggles { get; }
}
