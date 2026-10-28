namespace MiniMetrics.Services;

// The OS-level operations StartupManager reconciles. Split out so the reconciliation
// decisions can be tested without touching the registry or Task Scheduler.
public interface IStartupOperations
{
    // The value stored in the run-key, or null when the value is absent.
    string? ReadRunKeyPath();

    void WriteRunKey(string value);

    void RemoveRunKey();

    bool TaskExists();

    // Returns false if the elevation prompt was declined or the operation failed.
    bool CreateTask(string exePath);

    // Brings an existing task's run settings up to date (tasks made by older versions use Task Scheduler's
    // defaults). Best effort: it needs elevation and does nothing when the settings already match.
    void ApplyTaskSettings();

    // Returns false if the elevation prompt was declined or the operation failed.
    bool RemoveTask();
}
