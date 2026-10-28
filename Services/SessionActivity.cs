using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace MiniMetrics.Services;

// Reports whether anyone can see the desktop: false while the session is locked or its display is off,
// so the app can stop polling sensors nobody is looking at. Listens through one window's message hook.
// The session display status (not the console's) is used so a dark physical monitor does not pause a
// remote session.
[SupportedOSPlatform("windows")]
public sealed class SessionActivity
{
    private const uint WmWtsSessionChange = 0x02B1;
    private const uint WmPowerBroadcast = 0x0218;
    private const int WtsSessionLock = 0x7;
    private const int WtsSessionUnlock = 0x8;
    private const int PbtPowerSettingChange = 0x8013;
    private const int NotifyForThisSession = 0;
    private const int DeviceNotifyWindowHandle = 0;
    private const int DisplayOff = 0;

    private static readonly Guid SessionDisplayStatus = new("2b84c20e-ad23-4ddf-93db-05ffbd7efca5");

    private bool _locked;
    private bool _displayOff;
    private IntPtr _powerNotification;

    // Raised with the new state whenever it flips.
    public event Action<bool>? ActiveChanged;

    public bool IsActive => !_locked && !_displayOff;

    // Registers for lock and display notifications on the window, whose native handle exists from
    // construction. Unregisters when the window closes.
    public void Attach(Window window)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero) return;

        Win32Properties.AddWndProcHookCallback(window, OnMessage);
        WTSRegisterSessionNotification(hwnd, NotifyForThisSession);
        var guid = SessionDisplayStatus;
        _powerNotification = RegisterPowerSettingNotification(hwnd, ref guid, DeviceNotifyWindowHandle);

        window.Closed += (_, _) =>
        {
            WTSUnRegisterSessionNotification(hwnd);
            if (_powerNotification != IntPtr.Zero) UnregisterPowerSettingNotification(_powerNotification);
        };
    }

    private IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        bool wasActive = IsActive;

        if (msg == WmWtsSessionChange)
        {
            int change = wParam.ToInt32();
            if (change == WtsSessionLock) _locked = true;
            else if (change == WtsSessionUnlock) _locked = false;
        }
        else if (msg == WmPowerBroadcast && wParam.ToInt32() == PbtPowerSettingChange && lParam != IntPtr.Zero)
        {
            // POWERBROADCAST_SETTING: the setting GUID, a DWORD data length, then the data (0 off, 1 on,
            // 2 dimmed). Registration delivers the current state once right away.
            var setting = Marshal.PtrToStructure<PowerBroadcastSetting>(lParam);
            if (setting.PowerSetting == SessionDisplayStatus) _displayOff = setting.Data == DisplayOff;
        }

        if (IsActive != wasActive) ActiveChanged?.Invoke(IsActive);
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerBroadcastSetting
    {
        public Guid PowerSetting;
        public uint DataLength;
        public uint Data;
    }

    [DllImport("wtsapi32.dll")]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int flags);

    [DllImport("wtsapi32.dll")]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

    [DllImport("user32.dll")]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
}
