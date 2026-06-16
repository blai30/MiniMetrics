using Avalonia.Controls;
using Avalonia.Input;

namespace MiniMetrics.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // When locked, the window is click-through (handled in DesktopWindow), so this guards
    // the unlocked case where a press on the panel should start a window move.
    public bool IsLocked { get; set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsLocked && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
