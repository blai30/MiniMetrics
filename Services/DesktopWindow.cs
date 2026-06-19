using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace MiniMetrics.Services;

// Encapsulates the Windows-only window behavior: no taskbar/alt-tab entry, never steals focus,
// always pinned to the bottom of the z-order, and optional click-through. No-op off Windows.
public sealed class DesktopWindow(Window window)
{
    private const int GwlExstyle = -20;
    private const uint WsExToolwindow = 0x00000080;
    private const uint WsExNoactivate = 0x08000000;
    private const uint WsExTransparent = 0x00000020;

    private const uint WmWindowposchanging = 0x0046;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNozorder = 0x0004;
    private const uint SwpNoactivate = 0x0010;

    private static readonly IntPtr HwndBottom = new(1);
    private static readonly IntPtr HwndTopmost = new(-1);

    private bool _alwaysOnTop;

    // Registers the creation-time style callback and the wndproc hook. Call before Show().
    public void Attach()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Remove the window from the taskbar and alt-tab, and stop it ever taking focus.
        Win32Properties.AddWindowStylesCallback(window, (style, exStyle) =>
            (style, exStyle | WsExToolwindow | WsExNoactivate));

        // Force every z-order change to the widget's pinned band: the bottom by default, so it
        // sits above the wallpaper but below all other windows like a Rainmeter skin, or the
        // topmost band when always-on-top is enabled.
        Win32Properties.AddWndProcHookCallback(window,
            (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg != WmWindowposchanging) return IntPtr.Zero;
                var pos = Marshal.PtrToStructure<Windowpos>(lParam);
                pos.hwndInsertAfter = _alwaysOnTop ? HwndTopmost : HwndBottom;
                pos.flags &= ~SwpNozorder;
                Marshal.StructureToPtr(pos, lParam, false);

                return IntPtr.Zero;
            });
    }

    // Pins the widget to either the bottom of the z-order (a desktop skin) or the topmost band
    // (floating above all other windows). Updates the hook's target and applies it once now.
    public void SetAlwaysOnTop(bool enabled)
    {
        _alwaysOnTop = enabled;
        if (!OperatingSystem.IsWindows() || !TryGetHandle(out IntPtr hwnd)) return;

        IntPtr insertAfter = enabled ? HwndTopmost : HwndBottom;
        SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate);
    }

    // When enabled, the mouse passes through the widget to the desktop beneath it.
    public void SetClickThrough(bool enabled)
    {
        if (!OperatingSystem.IsWindows() || !TryGetHandle(out IntPtr hwnd)) return;

        long exStyle = GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();
        exStyle = enabled
            ? exStyle | WsExTransparent
            : exStyle & ~(long)WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExstyle, new(exStyle));
    }

    private bool TryGetHandle(out IntPtr hwnd)
    {
        if (window.TryGetPlatformHandle() is { } handle && handle.Handle != IntPtr.Zero)
        {
            hwnd = handle.Handle;
            return true;
        }

        hwnd = IntPtr.Zero;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Windowpos
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
