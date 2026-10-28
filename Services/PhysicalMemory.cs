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

    // The OS-usable physical memory total and the portion currently available, or null when it cannot
    // be read. This is the same GlobalMemoryStatusEx call LibreHardwareMonitor's "Total Memory" node
    // makes, read directly so its memory group (which also polls DIMMs over SMBus) never loads.
    public static (ulong TotalBytes, ulong AvailableBytes)? Usage()
    {
        if (!OperatingSystem.IsWindows()) return null;

        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? (status.TotalPhys, status.AvailPhys) : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
