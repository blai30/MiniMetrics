using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using MiniMetrics.Lib;
using MiniMetrics.Services;

namespace MiniMetrics.Views;

// One widget window's saved position. Save debounces through the settings controller; SaveNow forces
// an immediate write, used after an on-screen correction or the GPU's first placement.
public sealed class PositionSlot(Func<(int X, int Y)?> read, Action<int, int> save, Action flush)
{
    public (int X, int Y)? Saved => read();

    public void Save(int x, int y) => save(x, y);

    public void SaveNow(int x, int y)
    {
        save(x, y);
        flush();
    }
}

// Bundles one desktop widget: its overlay window, the Win32 desktop integration, position
// persistence, edge snapping against peers, and on-screen recovery. App holds one host per widget
// and drives them uniformly through this interface instead of repeating the wiring three times.
public sealed class WidgetHost(OverlayWindow window, PositionSlot position)
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

        if (position.Saved is { } saved) window.Position = new PixelPoint(saved.X, saved.Y);

        window.PositionChanged += (_, _) => position.Save(window.Position.X, window.Position.Y);

        window.Opened += (_, _) =>
        {
            if (position.Saved is null) OnFirstPlacement?.Invoke();

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
        window.Position = new PixelPoint(x, y);
        position.SaveNow(window.Position.X, window.Position.Y);
    }

    // If the restored position lands off every monitor, pull it onto the primary screen and persist.
    private void EnsureOnScreen()
    {
        var screens = window.Screens;
        if (screens.All.Count == 0) return;

        if (screens.ScreenFromPoint(window.Position) is not null) return;
        var primary = screens.Primary ?? screens.All[0];
        var area = primary.WorkingArea;
        window.Position = new PixelPoint(area.X + 48, area.Y + 48);
        position.SaveNow(window.Position.X, window.Position.Y);
    }

    private static EdgeSnap.Rect RectOf(Window window)
    {
        double scale = window.RenderScaling;
        return new EdgeSnap.Rect(
            window.Position.X,
            window.Position.Y,
            (int)Math.Round(window.Width * scale),
            (int)Math.Round(window.Height * scale));
    }
}
