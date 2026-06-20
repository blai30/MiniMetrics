using Avalonia.Input;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

public partial class DateTimeWindow : OverlayWindow
{
    public DateTimeWindow()
    {
        InitializeComponent();
    }

    protected override double FullWidth => 640;
    protected override double FullHeight => 176;

    // Hovering temporarily swaps the clock to its hover format pair. When the widget is locked it is
    // click-through, so the OS delivers no pointer events here and the clock stays on the normal pair.
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (DataContext is DateTimeWidgetViewModel vm) vm.IsHovering = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (DataContext is DateTimeWidgetViewModel vm) vm.IsHovering = false;
    }

    // Locking makes the window click-through, so OnPointerExited will never fire to clear a hover that
    // is active at that moment. Drop the hover now so the clock returns to its normal format pair.
    protected override void OnLockChanged(bool locked)
    {
        if (locked && DataContext is DateTimeWidgetViewModel vm) vm.IsHovering = false;
    }
}
