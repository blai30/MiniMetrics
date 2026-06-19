using MiniMetrics.Services;

namespace MiniMetrics.Tests;

// In-memory IStartupOperations that records calls so reconciliation decisions can be asserted.
internal sealed class FakeStartupOperations : IStartupOperations
{
    public string? RunKeyPath;
    public bool TaskPresent;
    public bool CreateTaskSucceeds = true;
    public bool RemoveTaskSucceeds = true;

    public int WriteRunKeyCalls;
    public int RemoveRunKeyCalls;
    public int CreateTaskCalls;
    public int RemoveTaskCalls;

    public string? ReadRunKeyPath() => RunKeyPath;

    public void WriteRunKey(string value)
    {
        RunKeyPath = value;
        WriteRunKeyCalls++;
    }

    public void RemoveRunKey()
    {
        RunKeyPath = null;
        RemoveRunKeyCalls++;
    }

    public bool TaskExists() => TaskPresent;

    public bool CreateTask(string exePath)
    {
        CreateTaskCalls++;
        if (!CreateTaskSucceeds) return false;

        TaskPresent = true;
        return true;
    }

    public bool RemoveTask()
    {
        RemoveTaskCalls++;
        if (!RemoveTaskSucceeds) return false;

        TaskPresent = false;
        return true;
    }
}
