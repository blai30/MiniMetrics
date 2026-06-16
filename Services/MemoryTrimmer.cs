using System;
using System.Runtime.InteropServices;

namespace MiniMetrics.Services;

// Trims the process working set by asking Windows to evict idle pages from physical RAM.
// The committed memory is unchanged; pages fault back in on demand. For a near-idle widget
// this keeps the resident footprint (what Task Manager reports) a fraction of the commit.
public static class MemoryTrimmer
{
    public static void Trim()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // A failure here is purely cosmetic (memory stays where it is), so it is ignored.
        EmptyWorkingSet(GetCurrentProcess());
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
}
