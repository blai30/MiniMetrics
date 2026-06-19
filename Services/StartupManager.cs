namespace MiniMetrics.Services;

// Reconciles the OS so exactly one of {scheduled task, run key, neither} is registered, matching
// the desired state. The task is the elevated path; the run key is the user-level path.
public sealed class StartupManager(IStartupOperations ops, string exePath)
{
    private readonly string _runKeyValue = $"\"{exePath}\"";

    // True when the app is registered to start at logon by either mechanism.
    public bool IsEnabled() => ops.TaskExists() || ops.ReadRunKeyPath() is not null;

    // Reconciles registration to (enabled, requiresElevation). Returns false if a required
    // elevation prompt was declined, in which case the prior registration is left untouched.
    public bool Sync(bool enabled, bool requiresElevation)
    {
        bool wantTask = enabled && requiresElevation;
        bool wantRunKey = enabled && !requiresElevation;

        switch (wantTask)
        {
            // Reconcile the task (elevated) side first so a declined prompt leaves the prior
            // registration in place rather than half-applied.
            case true when !ops.TaskExists() && !ops.CreateTask(exePath) && !ops.CreateTask(exePath):
            case false when ops.TaskExists() && !ops.RemoveTask() && !ops.RemoveTask():
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
