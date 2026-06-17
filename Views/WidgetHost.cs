using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using MiniMetrics.Lib;
using MiniMetrics.Services;

namespace MiniMetrics.Views;

// One widget window's saved position. Save debounces through the settings controller; SaveNow forces
// an immediate write, used after an on-screen correction or the GPU's first placement.
public sealed class PositionSlot
{
    private readonly Func<(int X, int Y)?> _read;
    private readonly Action<int, int> _save;
    private readonly Action _flush;

    public PositionSlot(Func<(int X, int Y)?> read, Action<int, int> save, Action flush)
    {
        _read = read;
        _save = save;
        _flush = flush;
    }

    public (int X, int Y)? Saved => _read();

    public void Save(int x, int y) => _save(x, y);

    public void SaveNow(int x, int y)
    {
        _save(x, y);
        _flush();
    }
}

// Bundles one desktop widget: its overlay window, the Win32 desktop integration, position
// persistence, edge snapping against peers, and on-screen recovery. App holds one host per widget
// and drives them uniformly through this interface instead of repeating the wiring three times.
public sealed class WidgetHost
{
    private readonly OverlayWindow _window;
    private readonly DesktopWindow _desktop;
    private readonly PositionSlot _position;
    private bool _alwaysOnTop;

    public WidgetHost(OverlayWindow window, PositionSlot position)
    {
        _window = window;
        _desktop = new DesktopWindow(window);
        _position = position;
    }

    // Runs the first time the window opens, before on-screen recovery, only when no position has been
    // saved yet. Lets the GPU widget seat itself beside the CPU widget on first appearance.
    public Action? OnFirstPlacement { get; set; }

    public bool IsVisible => _window.IsVisible;

    // The underlying window, needed only to nominate the desktop lifetime's MainWindow.
    public Window Window => _window;

    // Physical-pixel rectangle for edge snapping and peer placement.
    public EdgeSnap.Rect Rect => RectOf(_window);

    // Registers the desktop hook, restores the saved position, applies the current chrome flags, and
    // wires position persistence and on-screen recovery. Call once before the first Show.
    public void Initialize(bool locked, bool snapEnabled, bool alwaysOnTop)
    {
        _alwaysOnTop = alwaysOnTop;
        _window.IsLocked = locked;
        _window.SnapEnabled = snapEnabled;

        _desktop.Attach();
        _desktop.SetAlwaysOnTop(alwaysOnTop);

        if (_position.Saved is { } saved)
        {
            _window.Position = new PixelPoint(saved.X, saved.Y);
        }

        _window.PositionChanged += (_, _) => _position.Save(_window.Position.X, _window.Position.Y);

        _window.Opened += (_, _) =>
        {
            if (_position.Saved is null)
            {
                OnFirstPlacement?.Invoke();
            }

            EnsureOnScreen();
            _desktop.SetAlwaysOnTop(_alwaysOnTop);
            _desktop.SetClickThrough(_window.IsLocked);
        };
    }

    // Snaps this widget against the given peers during a drag, but only those currently shown.
    public void SnapAgainst(params WidgetHost[] peers)
    {
        _window.PeerRects = () =>
        {
            var rects = new List<EdgeSnap.Rect>(peers.Length);
            foreach (WidgetHost peer in peers)
            {
                if (peer.IsVisible)
                {
                    rects.Add(peer.Rect);
                }
            }

            return rects;
        };
    }

    // Shows the widget and reasserts its z-order band, which a fresh Show can otherwise reset.
    public void Show()
    {
        _window.Show();
        _desktop.SetAlwaysOnTop(_alwaysOnTop);
    }

    public void Hide() => _window.Hide();

    public void SetLocked(bool locked)
    {
        _window.IsLocked = locked;
        _desktop.SetClickThrough(locked);
    }

    public void SetAlwaysOnTop(bool onTop)
    {
        _alwaysOnTop = onTop;
        _desktop.SetAlwaysOnTop(onTop);
    }

    public void SetSnapEnabled(bool enabled) => _window.SnapEnabled = enabled;

    // Moves the widget to an absolute position and persists it immediately.
    public void MoveTo(int x, int y)
    {
        _window.Position = new PixelPoint(x, y);
        _position.SaveNow(_window.Position.X, _window.Position.Y);
    }

    // If the restored position lands off every monitor, pull it onto the primary screen and persist.
    private void EnsureOnScreen()
    {
        var screens = _window.Screens;
        if (screens is null || screens.All.Count == 0)
        {
            return;
        }

        if (screens.ScreenFromPoint(_window.Position) is null)
        {
            var primary = screens.Primary ?? screens.All[0];
            PixelRect area = primary.WorkingArea;
            _window.Position = new PixelPoint(area.X + 48, area.Y + 48);
            _position.SaveNow(_window.Position.X, _window.Position.Y);
        }
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
