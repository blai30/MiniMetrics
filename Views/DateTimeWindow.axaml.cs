using Avalonia.Input;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

public partial class DateTimeWindow : OverlayWindow
{
    public DateTimeWindow()
    {
        InitializeComponent();
    }

    // Hovering temporarily swaps the clock to its hover format pair. When the widget is locked it is
    // click-through, so the OS delivers no pointer events here and the clock stays on the normal pair.
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (DataContext is DateTimeWidgetViewModel vm)
        {
            vm.IsHovering = true;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (DataContext is DateTimeWidgetViewModel vm)
        {
            vm.IsHovering = false;
        }
    }
}
