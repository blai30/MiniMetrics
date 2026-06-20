using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

public partial class MetricWidgetWindow : OverlayWindow
{
    public MetricWidgetWindow()
    {
        InitializeComponent();
    }

    protected override double FullWidth => 210;
    protected override double FullHeight => 176;

    // The compute and memory cards can change membership after open (the GPU widget's rows arrive on the
    // first snapshot), so a change to either re-fits the compact width.
    protected override bool IsAutoWidthTrigger(string? propertyName) =>
        propertyName is nameof(MetricWidgetViewModel.Compute) or nameof(MetricWidgetViewModel.Memory);
}
