using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32;
using MiniMetrics.Lib;

namespace MiniMetrics.Services;

// Concrete startup operations for Windows: a per-user registry Run value for the non-elevated
// path and a "highest privileges" scheduled task for the elevated path.
[SupportedOSPlatform("windows")]
public sealed class WindowsStartupOperations : IStartupOperations
{
    private const string RunKeySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MiniMetrics";
    private const string TaskName = @"MiniMetrics\Autostart";

    public string? ReadRunKeyPath()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeySubKey);
        return key?.GetValue(ValueName) as string;
    }

    public void WriteRunKey(string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeySubKey);
        key.SetValue(ValueName, value, RegistryValueKind.String);
    }

    public void RemoveRunKey()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeySubKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public bool TaskExists()
    {
        // schtasks /Query exits 0 when the task exists, non-zero when it does not.
        return RunSchtasks($"/Query /TN \"{TaskName}\"", elevated: false) == 0;
    }

    public bool CreateTask(string exePath)
    {
        string arguments =
            $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST /F";
        if (RunSchtasks(arguments, elevated: true) != 0)
        {
            return false;
        }

        GrantCurrentUserDelete();
        return true;
    }

    // New tasks grant the user delete rights, so the non-elevated delete succeeds with no prompt. Tasks
    // created by older versions are admin-only, so fall back to the elevated path (one UAC prompt).
    public bool RemoveTask()
    {
        return RemoveTaskNonElevated()
            || RunSchtasks($"/Delete /TN \"{TaskName}\" /F", elevated: true) == 0;
    }

    // Deletes the task without elevating or showing UI. Used by the uninstall FastCallback, which must not
    // show a UAC prompt and is terminated after 30 seconds.
    public bool RemoveTaskNonElevated()
    {
        return RunSchtasks($"/Delete /TN \"{TaskName}\" /F", elevated: false) == 0;
    }

    // Grants the current user delete rights on the just-created task via the Task Scheduler COM API, so a
    // later non-elevated uninstall can remove it. Best-effort: task creation only runs while elevated, but if
    // setting the descriptor fails the task is still created and works, just admin-only-deletable as before.
    private static void GrantCurrentUserDelete()
    {
        try
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect();
            dynamic folder = service.GetFolder(@"\MiniMetrics");
            dynamic task = folder.GetTask("Autostart");

            const int daclSecurityInformation = 0x4;
            string existing = task.GetSecurityDescriptor(daclSecurityInformation);
            string updated = AutostartTaskSecurity.GrantUserDelete(existing, WindowsIdentity.GetCurrent().User!);
            if (updated != existing)
            {
                task.SetSecurityDescriptor(updated, 0);
            }
        }
        catch
        {
            // See method summary: descriptor update is best-effort.
        }
    }

    // Runs schtasks.exe and returns its exit code, or -1 if the call could not start or the
    // elevation prompt was declined. Elevated calls use the runas verb (one UAC prompt).
    private static int RunSchtasks(string arguments, bool elevated)
    {
        var info = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = elevated,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        if (elevated)
        {
            info.Verb = "runas";
        }
        else
        {
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
        }

        try
        {
            using Process? process = Process.Start(info);
            if (process is null)
            {
                return -1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            // Includes ERROR_CANCELLED (1223) when the user dismisses the UAC prompt.
            return -1;
        }
    }
}
