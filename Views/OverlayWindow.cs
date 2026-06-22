using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MiniMetrics.Lib;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

// Shared base for the desktop widgets: a draggable, edge-snapping borderless window that owns the
// full/compact layout lifecycle. Off Windows the move is handed to the OS (no snapping); on Windows
// the cursor is tracked so the widget can be pulled flush to screen edges and to peer widgets.
public abstract class OverlayWindow : Window
{
    private bool _dragging;
    private PixelPoint _windowStart;
    private PixelPoint _cursorStart;

    private bool _isLocked;

    // When locked, the window is click-through (handled in DesktopWindow), so this guards the
    // unlocked case where a press on the panel should start a window move.
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            _isLocked = value;
            OnLockChanged(value);
        }
    }

    // Hook for subclasses to react when the lock state changes. A locked window is click-through, so
    // pointer-driven state (e.g. hover) can no longer be cleared by the OS and must be reset here.
    protected virtual void OnLockChanged(bool locked)
    {
    }

    // The widget's full (non-compact) window size in code; compact mode hugs its content instead.
    protected abstract double FullWidth { get; }
    protected abstract double FullHeight { get; }

    // Compact mode is a single short row whose width tracks its content; the height is shared.
    private const double CompactHeight = 80;

    protected OverlayWindow()
    {
        // A widget whose content is already present when it first opens (the GPU widget shown reactively
        // once a snapshot confirms a GPU, the clock shown from the tray after it is already ticking) gets
        // no later measure pass, so SizeToContent never grows it. Force one once it is open.
        Opened += (_, _) => Dispatcher.UIThread.Post(ReapplyAutoWidth, DispatcherPriority.Loaded);
    }

    // Apply the current compact state when the view model is attached, and keep reacting to changes.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not ICompactWidget widget) return;
        widget.PropertyChanged += OnWidgetPropertyChanged;
        ApplyCompact(widget.IsCompact);
    }

    private void OnWidgetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not ICompactWidget widget) return;

        if (e.PropertyName == nameof(ICompactWidget.IsCompact))
            ApplyCompact(widget.IsCompact);
        else if (widget.IsCompact && IsAutoWidthTrigger(e.PropertyName))
            // The compact content changed membership (e.g. the GPU widget's rows arrived on the first
            // snapshot); re-fit the width once the new content has been laid out.
            Dispatcher.UIThread.Post(ReapplyAutoWidth, DispatcherPriority.Loaded);
    }

    // Names the view-model properties whose change should re-fit the compact width. Default: none beyond
    // IsCompact; a widget whose compact row can gain or lose content after open overrides this.
    protected virtual bool IsAutoWidthTrigger(string? propertyName) => false;

    // Forces a fresh measure pass so SizeToContent recomputes the width against the realized content.
    private void ReapplyAutoWidth()
    {
        if (DataContext is not ICompactWidget { IsCompact: true }) return;
        SizeToContent = SizeToContent.Width;
        InvalidateMeasure();
    }

    // Compact: a single CompactHeight-tall row whose width tracks its content. Full: the fixed size.
    private void ApplyCompact(bool compact)
    {
        if (compact)
        {
            SizeToContent = SizeToContent.Width;
            Height = CompactHeight;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            Width = FullWidth;
            Height = FullHeight;
        }
    }

    // When enabled, a drag pulls the widget flush to nearby screen edges and peer widgets.
    public bool SnapEnabled { get; set; }

    // Supplies the rectangles (physical pixels) of other widgets to snap against during a drag.
    public Func<IReadOnlyList<EdgeSnap.Rect>>? PeerRects { get; set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (IsLocked || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        // Off Windows, hand the move to the OS (no snapping); GetCursorPos is Windows-only.
        if (!OperatingSystem.IsWindows())
        {
            BeginMoveDrag(e);
            return;
        }

        if (TryGetCursorPos(out var cursor))
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

        if (!_dragging || !TryGetCursorPos(out var cursor)) return;

        var desired = _windowStart + (cursor - _cursorStart);
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

    // Converts the widget's logical size to physical pixels and snaps against the working area of
    // the screen under the cursor and any peer widgets (falls back to no snap if screen unknown).
    private PixelPoint ApplySnap(PixelPoint desired)
    {
        var screen = Screens.ScreenFromPoint(desired);
        if (screen is null) return desired;

        double scale = RenderScaling;
        var widget = new EdgeSnap.Rect(
            desired.X,
            desired.Y,
            (int)Math.Round(Width * scale),
            (int)Math.Round(Height * scale));

        var area = screen.WorkingArea;
        var workArea = new EdgeSnap.Rect(area.X, area.Y, area.Width, area.Height);

        var peers = PeerRects?.Invoke() ?? [];

        (int x, int y) = EdgeSnap.Snap(widget, workArea, peers, SnapThreshold);
        return new(x, y);
    }

    // Snap pull distance in physical pixels.
    private const int SnapThreshold = 10;

    private static bool TryGetCursorPos(out PixelPoint point)
    {
        if (OperatingSystem.IsWindows() && GetCursorPos(out var native))
        {
            point = new(native.X, native.Y);
            return true;
        }

        point = default;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);
}
