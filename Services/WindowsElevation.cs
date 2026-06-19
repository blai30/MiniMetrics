using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace MiniMetrics.Services;

// Real elevation on Windows: reads the process token for the administrator role, and relaunches the
// app elevated via ShellExecute with the runas verb (one UAC prompt). Verified manually on Windows.
[SupportedOSPlatform("windows")]
public sealed class WindowsElevation : IElevation
{
    public bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool RelaunchElevated(string exePath)
    {
        var info = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            // A non-null Process means the elevated instance started; the caller then exits this one.
            return Process.Start(info) is not null;
        }
        catch (Win32Exception)
        {
            // Includes ERROR_CANCELLED (1223) when the user dismisses the UAC prompt.
            return false;
        }
    }
}
