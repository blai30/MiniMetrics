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
    private const string TaskFolderPath = @"\MiniMetrics";
    private const string TaskFolderName = "MiniMetrics";
    private const string TaskLeafName = "Autostart";

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
        if (RemoveTaskNonElevated())
        {
            return true;
        }

        bool removed = RunSchtasks($"/Delete /TN \"{TaskName}\" /F", elevated: true) == 0;
        if (removed)
        {
            TryRemoveTaskFolder();
        }
        return removed;
    }

    // Deletes the task without elevating or showing UI. Used by the uninstall FastCallback, which must not
    // show a UAC prompt and is terminated after 30 seconds.
    public bool RemoveTaskNonElevated()
    {
        if (RunSchtasks($"/Delete /TN \"{TaskName}\" /F", elevated: false) != 0)
        {
            return false;
        }

        TryRemoveTaskFolder();
        return true;
    }

    // Grants the current user delete rights on the just-created task and its containing folder via the Task
    // Scheduler COM API, so a later non-elevated uninstall can remove both with no prompt. Best-effort: task
    // creation only runs while elevated, but if setting a descriptor fails the task is still created and
    // works, just admin-only-deletable as before.
    private static void GrantCurrentUserDelete()
    {
        try
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect();
            var user = WindowsIdentity.GetCurrent().User!;

            dynamic folder = service.GetFolder(TaskFolderPath);
            GrantDelete(folder, user);

            dynamic task = folder.GetTask(TaskLeafName);
            GrantDelete(task, user);
        }
        catch
        {
            // See method summary: descriptor update is best-effort.
        }
    }

    // Adds a delete-only ACE for the user to a task or task-folder security descriptor; both COM objects
    // expose the same get/set methods. No-op when the user already holds the rights.
    private static void GrantDelete(dynamic securable, SecurityIdentifier user)
    {
        const int daclSecurityInformation = 0x4;
        string existing = securable.GetSecurityDescriptor(daclSecurityInformation);
        string updated = AutostartTaskSecurity.GrantUserDelete(existing, user);
        if (updated != existing)
        {
            securable.SetSecurityDescriptor(updated, 0);
        }
    }

    // Removes the task's containing folder once the task itself is gone. schtasks deletes the task but leaves
    // the empty folder behind. Best-effort and non-elevated: new installs grant the user delete rights on the
    // folder so this succeeds silently; if it cannot (a folder from an older version, or a non-empty folder),
    // the empty folder simply remains, which is harmless.
    private static void TryRemoveTaskFolder()
    {
        try
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect();
            dynamic root = service.GetFolder(@"\");
            root.DeleteFolder(TaskFolderName, 0);
        }
        catch
        {
            // Best-effort: leaving an empty folder behind is harmless.
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
