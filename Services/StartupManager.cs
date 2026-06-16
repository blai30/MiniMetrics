namespace MiniMetrics.Services;

// Reconciles the OS so exactly one of {scheduled task, run key, neither} is registered, matching
// the desired state. The task is the elevated path; the run key is the user-level path.
public sealed class StartupManager
{
    private readonly IStartupOperations _ops;
    private readonly string _exePath;
    private readonly string _runKeyValue;

    public StartupManager(IStartupOperations ops, string exePath)
    {
        _ops = ops;
        _exePath = exePath;
        _runKeyValue = $"\"{exePath}\"";
    }

    // True when the app is registered to start at logon by either mechanism.
    public bool IsEnabled() => _ops.TaskExists() || _ops.ReadRunKeyPath() is not null;

    // Reconciles registration to (enabled, requiresElevation). Returns false if a required
    // elevation prompt was declined, in which case the prior registration is left untouched.
    public bool Sync(bool enabled, bool requiresElevation)
    {
        bool wantTask = enabled && requiresElevation;
        bool wantRunKey = enabled && !requiresElevation;

        // Reconcile the task (elevated) side first so a declined prompt leaves the prior
        // registration in place rather than half-applied.
        if (wantTask && !_ops.TaskExists())
        {
            if (!_ops.CreateTask(_exePath))
            {
                return false;
            }
        }
        else if (!wantTask && _ops.TaskExists())
        {
            if (!_ops.RemoveTask())
            {
                return false;
            }
        }

        // The run-key side never elevates and never fails.
        if (wantRunKey)
        {
            if (_ops.ReadRunKeyPath() != _runKeyValue)
            {
                _ops.WriteRunKey(_runKeyValue);
            }
        }
        else if (_ops.ReadRunKeyPath() is not null)
        {
            _ops.RemoveRunKey();
        }

        return true;
    }

    // Rewrites the run-key value if it exists but points to a stale path. Never touches the task
    // and never elevates, so it is safe to call at launch.
    public void RefreshRunKeyPath()
    {
        string? current = _ops.ReadRunKeyPath();
        if (current is not null && current != _runKeyValue)
        {
            _ops.WriteRunKey(_runKeyValue);
        }
    }
}
