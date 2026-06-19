using System;
using System.Runtime.InteropServices;

namespace MiniMetrics.Services;

// The installed physical memory the firmware reports (SMBIOS), which is larger than the memory the OS
// can address: the chipset reserves a slice below the installed total for the BIOS/ACPI, PCI/MMIO
// apertures, and any integrated-GPU framebuffer. GlobalMemoryStatusEx (the source behind
// LibreHardwareMonitor's "Memory Used"/"Memory Available") reports only the usable remainder, so this
// is the figure to use when the widget should show the full installed size the user sees on their spec
// sheet.
internal static class PhysicalMemory
{
    // Installed memory in bytes, or 0 when it cannot be read (non-Windows, or the firmware call fails),
    // which the caller treats as "unknown" and falls back to the OS-usable total.
    public static ulong InstalledBytes()
    {
        if (!OperatingSystem.IsWindows()) return 0;

        return GetPhysicallyInstalledSystemMemory(out ulong totalKilobytes) ? totalKilobytes * 1024UL : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);
}
