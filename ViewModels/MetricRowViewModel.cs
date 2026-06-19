using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniMetrics.Lib;

namespace MiniMetrics.ViewModels;

public partial class MetricRowViewModel : ObservableObject
{
    [ObservableProperty] public partial string Key { get; set; } = "";
    [ObservableProperty] public partial string Label { get; set; } = "";
    [ObservableProperty] public partial string Value { get; set; } = "";
    [ObservableProperty] public partial string Temp { get; set; } = "";
    [ObservableProperty] public partial TempLevel TempLevel { get; set; }
    [ObservableProperty] public partial string Detail { get; set; } = "";
    [ObservableProperty] public partial double BarPercent { get; set; }
    [ObservableProperty] public partial RowColor Color { get; set; }
    [ObservableProperty] public partial bool IsVisible { get; set; } = true;

    // Element-level visibility for the compute cards (CPU, GPU). Hiding one keeps the others in
    // place: the view binds these to Opacity so the layout slot is preserved rather than collapsed.
    [ObservableProperty] public partial bool UsageVisible { get; set; } = true;
    [ObservableProperty] public partial bool TempVisible { get; set; } = true;
    [ObservableProperty] public partial bool PowerVisible { get; set; } = true;

    // The active font scale and resolved weights, stamped by the owning widget so the row template
    // (whose DataContext is this row) can bind them. Defaults reproduce the original look.
    [ObservableProperty] public partial double Scale { get; set; } = 1.0;
    [ObservableProperty] public partial FontWeight StrongWeight { get; set; } = FontWeight.Bold;
    [ObservableProperty] public partial FontWeight UnitWeight { get; set; } = FontWeight.SemiBold;

    // True when this row has a temperature to show, used to collapse the temp slot otherwise.
    public bool HasTemp => Temp.Length > 0;

    partial void OnTempChanged(string value) => OnPropertyChanged(nameof(HasTemp));

    // Re-raises the accent-bearing properties so the theme-aware converters re-evaluate after the
    // active theme variant changes. The underlying values are unchanged.
    public void NotifyThemeChanged()
    {
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(TempLevel));
    }
}
