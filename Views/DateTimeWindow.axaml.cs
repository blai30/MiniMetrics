using Avalonia.Input;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

public partial class DateTimeWindow : OverlayWindow
{
    public DateTimeWindow()
    {
        InitializeComponent();
    }

    // Hovering temporarily switches the clock to 24-hour. When the widget is locked it is
    // click-through, so the OS delivers no pointer events here and the clock stays 12-hour.
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (DataContext is DateTimeWidgetViewModel vm)
        {
            vm.Is24Hour = true;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (DataContext is DateTimeWidgetViewModel vm)
        {
            vm.Is24Hour = false;
        }
    }
}
