using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace MiniMetrics.Services;

// Encapsulates the Windows-only window behavior: no taskbar/alt-tab entry, never steals focus,
// always pinned to the bottom of the z-order, and optional click-through. No-op off Windows.
public sealed class DesktopWindow
{
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;

    private const uint WM_WINDOWPOSCHANGING = 0x0046;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static readonly IntPtr HWND_BOTTOM = new(1);
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private readonly Window _window;
    private bool _alwaysOnTop;

    public DesktopWindow(Window window) => _window = window;

    // Registers the creation-time style callback and the wndproc hook. Call before Show().
    public void Attach()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Remove the window from the taskbar and alt-tab, and stop it ever taking focus.
        Win32Properties.AddWindowStylesCallback(_window, (style, exStyle) =>
            (style, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));

        // Force every z-order change to the widget's pinned band: the bottom by default, so it
        // sits above the wallpaper but below all other windows like a Rainmeter skin, or the
        // topmost band when always-on-top is enabled.
        Win32Properties.AddWndProcHookCallback(_window,
            (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg == WM_WINDOWPOSCHANGING)
                {
                    var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    pos.hwndInsertAfter = _alwaysOnTop ? HWND_TOPMOST : HWND_BOTTOM;
                    pos.flags &= ~SWP_NOZORDER;
                    Marshal.StructureToPtr(pos, lParam, false);
                }

                return IntPtr.Zero;
            });
    }

    // Pins the widget to either the bottom of the z-order (a desktop skin) or the topmost band
    // (floating above all other windows). Updates the hook's target and applies it once now.
    public void SetAlwaysOnTop(bool enabled)
    {
        _alwaysOnTop = enabled;
        if (!OperatingSystem.IsWindows() || !TryGetHandle(out IntPtr hwnd))
        {
            return;
        }

        IntPtr insertAfter = enabled ? HWND_TOPMOST : HWND_BOTTOM;
        SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // When enabled, the mouse passes through the widget to the desktop beneath it.
    public void SetClickThrough(bool enabled)
    {
        if (!OperatingSystem.IsWindows() || !TryGetHandle(out IntPtr hwnd))
        {
            return;
        }

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        exStyle = enabled
            ? exStyle | WS_EX_TRANSPARENT
            : exStyle & ~(long)WS_EX_TRANSPARENT;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    private bool TryGetHandle(out IntPtr hwnd)
    {
        if (_window.TryGetPlatformHandle() is { } handle && handle.Handle != IntPtr.Zero)
        {
            hwnd = handle.Handle;
            return true;
        }

        hwnd = IntPtr.Zero;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
