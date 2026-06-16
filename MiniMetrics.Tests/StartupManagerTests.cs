using MiniMetrics.Services;
using Xunit;

namespace MiniMetrics.Tests;

public class StartupManagerTests
{
    private const string Exe = @"C:\Apps\MiniMetrics\MiniMetrics.exe";
    private const string Value = "\"" + Exe + "\"";

    private static (StartupManager Manager, FakeStartupOperations Ops) Build()
    {
        var ops = new FakeStartupOperations();
        return (new StartupManager(ops, Exe), ops);
    }

    [Fact]
    public void Enable_without_elevation_writes_run_key_only()
    {
        var (manager, ops) = Build();
        bool ok = manager.Sync(enabled: true, requiresElevation: false);

        Assert.True(ok);
        Assert.Equal(Value, ops.RunKeyPath);
        Assert.False(ops.TaskPresent);
    }

    [Fact]
    public void Enable_with_elevation_creates_task_only()
    {
        var (manager, ops) = Build();
        bool ok = manager.Sync(enabled: true, requiresElevation: true);

        Assert.True(ok);
        Assert.True(ops.TaskPresent);
        Assert.Null(ops.RunKeyPath);
    }

    [Fact]
    public void Disable_removes_run_key()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = Value;

        manager.Sync(enabled: false, requiresElevation: false);

        Assert.Null(ops.RunKeyPath);
    }

    [Fact]
    public void Disable_removes_task()
    {
        var (manager, ops) = Build();
        ops.TaskPresent = true;

        manager.Sync(enabled: false, requiresElevation: true);

        Assert.False(ops.TaskPresent);
    }

    [Fact]
    public void Enabling_elevation_migrates_run_key_to_task()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = Value;

        bool ok = manager.Sync(enabled: true, requiresElevation: true);

        Assert.True(ok);
        Assert.True(ops.TaskPresent);
        Assert.Null(ops.RunKeyPath);
    }

    [Fact]
    public void Disabling_elevation_migrates_task_to_run_key()
    {
        var (manager, ops) = Build();
        ops.TaskPresent = true;

        bool ok = manager.Sync(enabled: true, requiresElevation: false);

        Assert.True(ok);
        Assert.False(ops.TaskPresent);
        Assert.Equal(Value, ops.RunKeyPath);
    }

    [Fact]
    public void Cancelled_task_creation_leaves_run_key_untouched()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = Value;
        ops.CreateTaskSucceeds = false;

        bool ok = manager.Sync(enabled: true, requiresElevation: true);

        Assert.False(ok);
        Assert.Equal(Value, ops.RunKeyPath);
        Assert.False(ops.TaskPresent);
        Assert.Equal(0, ops.RemoveRunKeyCalls);
    }

    [Fact]
    public void Cancelled_task_removal_leaves_task_and_writes_no_run_key()
    {
        var (manager, ops) = Build();
        ops.TaskPresent = true;
        ops.RemoveTaskSucceeds = false;

        bool ok = manager.Sync(enabled: true, requiresElevation: false);

        Assert.False(ok);
        Assert.True(ops.TaskPresent);
        Assert.Null(ops.RunKeyPath);
        Assert.Equal(0, ops.WriteRunKeyCalls);
    }

    [Fact]
    public void Re_syncing_with_elevation_does_not_recreate_the_task()
    {
        // Enabling a second CPU temp/power metric re-syncs while the task already exists. The
        // task must not be recreated, so no second UAC prompt is raised.
        var (manager, ops) = Build();
        ops.TaskPresent = true;

        bool ok = manager.Sync(enabled: true, requiresElevation: true);

        Assert.True(ok);
        Assert.Equal(0, ops.CreateTaskCalls);
        Assert.True(ops.TaskPresent);
    }

    [Fact]
    public void Re_enabling_same_run_key_writes_nothing()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = Value;

        manager.Sync(enabled: true, requiresElevation: false);

        Assert.Equal(0, ops.WriteRunKeyCalls);
    }

    [Fact]
    public void RefreshRunKeyPath_rewrites_a_stale_path()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = "\"C:\\Old\\MiniMetrics.exe\"";

        manager.RefreshRunKeyPath();

        Assert.Equal(Value, ops.RunKeyPath);
    }

    [Fact]
    public void RefreshRunKeyPath_does_nothing_when_absent()
    {
        var (manager, ops) = Build();

        manager.RefreshRunKeyPath();

        Assert.Null(ops.RunKeyPath);
        Assert.Equal(0, ops.WriteRunKeyCalls);
    }

    [Fact]
    public void IsEnabled_true_when_task_present()
    {
        var (manager, ops) = Build();
        ops.TaskPresent = true;

        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void IsEnabled_false_when_neither_present()
    {
        var (manager, _) = Build();

        Assert.False(manager.IsEnabled());
    }
}
