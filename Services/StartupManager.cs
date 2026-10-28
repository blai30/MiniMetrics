namespace MiniMetrics.Services;

// Reconciles the OS so exactly one of {scheduled task, run key, neither} is registered, matching
// the desired state. The task is the elevated path; the run key is the user-level path.
public sealed class StartupManager(IStartupOperations ops, string exePath)
{
    private readonly string _runKeyValue = $"\"{exePath}\"";

    // True when the app is registered to start at logon by either mechanism. The registry read goes
    // first because the task query spawns schtasks.exe.
    public bool IsEnabled() => ops.ReadRunKeyPath() is not null || ops.TaskExists();

    // Reconciles registration to (enabled, requiresElevation). Returns false if a required
    // elevation prompt was declined, in which case the prior registration is left untouched.
    public bool Sync(bool enabled, bool requiresElevation)
    {
        bool wantTask = enabled && requiresElevation;
        bool wantRunKey = enabled && !requiresElevation;

        // Reconcile the task (elevated) side first so a declined prompt leaves the prior registration in
        // place rather than half-applied. Each create/remove is attempted once: both raise a UAC prompt
        // against the real OS, so a retry would re-prompt the user. A task that is kept gets its run
        // settings refreshed, which migrates one created by an older version.
        if (wantTask)
        {
            if (ops.TaskExists())
            {
                ops.ApplyTaskSettings();
            }
            else if (!ops.CreateTask(exePath))
            {
                return false;
            }
        }
        else if (ops.TaskExists() && !ops.RemoveTask())
        {
            return false;
        }

        // The run-key side never elevates and never fails.
        if (wantRunKey)
        {
            if (ops.ReadRunKeyPath() != _runKeyValue) ops.WriteRunKey(_runKeyValue);
        }
        else if (ops.ReadRunKeyPath() is not null)
        {
            ops.RemoveRunKey();
        }

        return true;
    }

    // Rewrites the run-key value if it exists but points to a stale path. Never touches the task
    // and never elevates, so it is safe to call at launch.
    public void RefreshRunKeyPath()
    {
        string? current = ops.ReadRunKeyPath();
        if (current is not null && current != _runKeyValue) ops.WriteRunKey(_runKeyValue);
    }
}
