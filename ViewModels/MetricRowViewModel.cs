using CommunityToolkit.Mvvm.ComponentModel;
using MiniMetrics.Lib;

namespace MiniMetrics.ViewModels;

public partial class MetricRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private string _label = "";

    [ObservableProperty]
    private string _value = "";

    [ObservableProperty]
    private string _temp = "";

    [ObservableProperty]
    private TempLevel _tempLevel;

    [ObservableProperty]
    private string _detail = "";

    [ObservableProperty]
    private double _barPercent;

    [ObservableProperty]
    private RowColor _color;

    [ObservableProperty]
    private bool _isVisible = true;

    // Element-level visibility for the compute cards (CPU, GPU). Hiding one keeps the others in
    // place: the view binds these to Opacity so the layout slot is preserved rather than collapsed.
    [ObservableProperty]
    private bool _usageVisible = true;

    [ObservableProperty]
    private bool _tempVisible = true;

    [ObservableProperty]
    private bool _powerVisible = true;

    // True when this row has a temperature to show, used to collapse the temp slot otherwise.
    public bool HasTemp => Temp.Length > 0;

    partial void OnTempChanged(string value) => OnPropertyChanged(nameof(HasTemp));
}
