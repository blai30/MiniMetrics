using System;
using System.Collections.Generic;
using Avalonia.Controls;
using MiniMetrics.Lib;
using MiniMetrics.Services;

namespace MiniMetrics.Views;

// Bundles one desktop widget: its overlay window, the Win32 desktop integration, position
// persistence, edge snapping against peers, and on-screen recovery. App holds one host per widget
// and drives them uniformly through this interface instead of repeating the wiring three times.
// readPosition reads the saved position; savePosition debounces through the settings controller; a
// SaveNow forces an immediate flush, used after an on-screen correction or the GPU's first placement.
public sealed class WidgetHost(
    OverlayWindow window,
    Func<(int X, int Y)?> readPosition,
    Action<int, int> savePosition,
    Action flushPosition)
{
    private readonly DesktopWindow _desktop = new(window);
    private bool _alwaysOnTop;

    // Runs the first time the window opens, before on-screen recovery, only when no position has been
    // saved yet. Lets the GPU widget seat itself beside the CPU widget on first appearance.
    public Action? OnFirstPlacement { get; set; }

    public bool IsVisible => window.IsVisible;

    // The underlying window, needed only to nominate the desktop lifetime's MainWindow.
    public Window Window => window;

    // Physical-pixel rectangle for edge snapping and peer placement.
    public EdgeSnap.Rect Rect => RectOf(window);

    // Registers the desktop hook, restores the saved position, applies the current chrome flags, and
    // wires position persistence and on-screen recovery. Call once before the first Show.
    public void Initialize(bool locked, bool snapEnabled, bool alwaysOnTop)
    {
        _alwaysOnTop = alwaysOnTop;
        window.IsLocked = locked;
        window.SnapEnabled = snapEnabled;

        _desktop.Attach();
        _desktop.SetAlwaysOnTop(alwaysOnTop);

        if (readPosition() is { } saved) window.Position = new(saved.X, saved.Y);

        window.PositionChanged += (_, _) => savePosition(window.Position.X, window.Position.Y);

        window.Opened += (_, _) =>
        {
            if (readPosition() is null) OnFirstPlacement?.Invoke();

            EnsureOnScreen();
            _desktop.SetAlwaysOnTop(_alwaysOnTop);
            _desktop.SetClickThrough(window.IsLocked);
        };
    }

    // Snaps this widget against the given peers during a drag, but only those currently shown.
    public void SnapAgainst(params WidgetHost[] peers)
    {
        window.PeerRects = () =>
        {
            var rects = new List<EdgeSnap.Rect>(peers.Length);
            foreach (var peer in peers)
                if (peer.IsVisible)
                    rects.Add(peer.Rect);

            return rects;
        };
    }

    // Shows the widget and reasserts its z-order band, which a fresh Show can otherwise reset.
    public void Show()
    {
        window.Show();
        _desktop.SetAlwaysOnTop(_alwaysOnTop);
    }

    public void Hide() => window.Hide();

    public void SetLocked(bool locked)
    {
        window.IsLocked = locked;
        _desktop.SetClickThrough(locked);
    }

    public void SetAlwaysOnTop(bool onTop)
    {
        _alwaysOnTop = onTop;
        _desktop.SetAlwaysOnTop(onTop);
    }

    public void SetSnapEnabled(bool enabled) => window.SnapEnabled = enabled;

    // Moves the widget to an absolute position and persists it immediately.
    public void MoveTo(int x, int y)
    {
        window.Position = new(x, y);
        SaveNow();
    }

    // Persists the current position and forces an immediate write, bypassing the debounce.
    private void SaveNow()
    {
        savePosition(window.Position.X, window.Position.Y);
        flushPosition();
    }

    // If the restored position lands off every monitor, pull it onto the primary screen and persist.
    private void EnsureOnScreen()
    {
        var screens = window.Screens;
        if (screens.All.Count == 0) return;

        if (screens.ScreenFromPoint(window.Position) is not null) return;
        var primary = screens.Primary ?? screens.All[0];
        var area = primary.WorkingArea;
        window.Position = new(area.X + 48, area.Y + 48);
        SaveNow();
    }

    private static EdgeSnap.Rect RectOf(Window window)
    {
        double scale = window.RenderScaling;
        return new(
            window.Position.X,
            window.Position.Y,
            (int)Math.Round(window.Width * scale),
            (int)Math.Round(window.Height * scale));
    }
}
