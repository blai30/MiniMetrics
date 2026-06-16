using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MiniMetrics.Lib;

namespace MiniMetrics.Views;

public partial class MainWindow : Window
{
    private bool _dragging;
    private PixelPoint _windowStart;
    private PixelPoint _cursorStart;

    public MainWindow()
    {
        InitializeComponent();
    }

    // When locked, the window is click-through (handled in DesktopWindow), so this guards
    // the unlocked case where a press on the panel should start a window move.
    public bool IsLocked { get; set; }

    // When enabled, a drag pulls the widget flush to nearby screen edges.
    public bool SnapEnabled { get; set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (IsLocked || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Off Windows, hand the move to the OS (no snapping); GetCursorPos is Windows-only.
        if (!OperatingSystem.IsWindows())
        {
            BeginMoveDrag(e);
            return;
        }

        if (TryGetCursorPos(out PixelPoint cursor))
        {
            _dragging = true;
            _cursorStart = cursor;
            _windowStart = Position;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging || !TryGetCursorPos(out PixelPoint cursor))
        {
            return;
        }

        PixelPoint desired = _windowStart + (cursor - _cursorStart);
        Position = SnapEnabled ? ApplySnap(desired) : desired;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    // Converts the widget's logical size to physical pixels and snaps against the
    // working area of the screen under the cursor (falls back to no snap if unknown).
    private PixelPoint ApplySnap(PixelPoint desired)
    {
        var screen = Screens.ScreenFromPoint(desired);
        if (screen is null)
        {
            return desired;
        }

        double scale = RenderScaling;
        var widget = new EdgeSnap.Rect(
            desired.X,
            desired.Y,
            (int)Math.Round(Width * scale),
            (int)Math.Round(Height * scale));

        PixelRect area = screen.WorkingArea;
        var workArea = new EdgeSnap.Rect(area.X, area.Y, area.Width, area.Height);

        (int x, int y) = EdgeSnap.Snap(widget, workArea, NoPeers, SnapThreshold);
        return new PixelPoint(x, y);
    }

    // Snap pull distance in physical pixels.
    private const int SnapThreshold = 10;
    private static readonly EdgeSnap.Rect[] NoPeers = System.Array.Empty<EdgeSnap.Rect>();

    private static bool TryGetCursorPos(out PixelPoint point)
    {
        if (OperatingSystem.IsWindows() && GetCursorPos(out POINT native))
        {
            point = new PixelPoint(native.X, native.Y);
            return true;
        }

        point = default;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);
}
