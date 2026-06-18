using System;

namespace MiniMetrics.Services;

public enum UninstallOutcome
{
    Completed,
    Aborted,
}

// Drives the in-app uninstall in the required order. The scheduled task is removed first because it is the
// only step that can require administrator rights (a task left by an older version is admin-only); if that
// is declined or fails, the whole uninstall aborts and nothing else is touched. Otherwise the per-user run
// key is removed and the platform uninstaller is launched to remove the install, shortcuts, and the
// Add/Remove Programs entry.
public sealed class UninstallCoordinator
{
    private readonly IStartupOperations _ops;
    private readonly Action _launchPlatformUninstaller;

    public UninstallCoordinator(IStartupOperations ops, Action launchPlatformUninstaller)
    {
        _ops = ops;
        _launchPlatformUninstaller = launchPlatformUninstaller;
    }

    public UninstallOutcome Run()
    {
        if (_ops.TaskExists() && !_ops.RemoveTask())
        {
            return UninstallOutcome.Aborted;
        }

        _ops.RemoveRunKey();
        _launchPlatformUninstaller();
        return UninstallOutcome.Completed;
    }
}
