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

    // True when this row has a temperature to show, used to collapse the temp slot otherwise.
    public bool HasTemp => Temp.Length > 0;

    // True when this row has trailing detail (power or memory total), used to collapse it otherwise.
    public bool HasDetail => Detail.Length > 0;

    partial void OnTempChanged(string value) => OnPropertyChanged(nameof(HasTemp));

    partial void OnDetailChanged(string value) => OnPropertyChanged(nameof(HasDetail));
}
