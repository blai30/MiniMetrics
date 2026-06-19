using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MiniMetrics.Services;

// Detects PawnIO by its uninstall registry key, the same signal LibreHardwareMonitor uses to find the
// driver. HKLM is readable without elevation, so this works from the unelevated launch gate before any
// decision to relaunch. Both registry views are checked so a 32-bit installer entry is still found.
// Verified manually on Windows.
[SupportedOSPlatform("windows")]
public sealed class WindowsDriverProbe : IDriverProbe
{
    private const string UninstallKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";

    public bool IsInstalled() =>
        KeyExists(RegistryView.Registry64) || KeyExists(RegistryView.Registry32);

    private static bool KeyExists(RegistryView view)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var key = hklm.OpenSubKey(UninstallKey);
        return key is not null;
    }
}
