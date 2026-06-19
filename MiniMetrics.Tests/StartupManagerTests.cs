using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class StartupManagerTests
{
    private const string Exe = @"C:\Apps\MiniMetrics\MiniMetrics.exe";
    private const string Value = "\"" + Exe + "\"";

    private static (StartupManager Manager, FakeStartupOperations Ops) Build()
    {
        var ops = new FakeStartupOperations();
        return (new StartupManager(ops, Exe), ops);
    }

    [TestMethod]
    public void Enable_without_elevation_writes_run_key_only()
    {
        var (manager, ops) = Build();
        bool ok = manager.Sync(true, false);

        Assert.IsTrue(ok);
        Assert.AreEqual(Value, ops.RunKeyPath);
        Assert.IsFalse(ops.TaskPresent);
    }

    [TestMethod]
    public void Enable_with_elevation_creates_task_only()
    {
        var (manager, ops) = Build();
        bool ok = manager.Sync(true, true);

        Assert.IsTrue(ok);
        Assert.IsTrue(ops.TaskPresent);
        Assert.IsNull(ops.RunKeyPath);
    }

    [TestMethod]
    public void Disable_removes_run_key()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = Value;

        manager.Sync(false, false);

        Assert.IsNull(ops.RunKeyPath);
    }

    [TestMethod]
    public void Disable_removes_task()
    {
        var (manager, ops) = Build();
        ops.TaskPresent = true;

        manager.Sync(false, true);

        Assert.IsFalse(ops.TaskPresent);
    }

    [TestMethod]
    public void Enabling_elevation_migrates_run_key_to_task()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = Value;

        bool ok = manager.Sync(true, true);

        Assert.IsTrue(ok);
        Assert.IsTrue(ops.TaskPresent);
        Assert.IsNull(ops.RunKeyPath);
    }

    [TestMethod]
    public void Disabling_elevation_migrates_task_to_run_key()
    {
        var (manager, ops) = Build();
        ops.TaskPresent = true;

        bool ok = manager.Sync(true, false);

        Assert.IsTrue(ok);
        Assert.IsFalse(ops.TaskPresent);
        Assert.AreEqual(Value, ops.RunKeyPath);
    }

    [TestMethod]
    public void Cancelled_task_creation_leaves_run_key_untouched()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = Value;
        ops.CreateTaskSucceeds = false;

        bool ok = manager.Sync(true, true);

        Assert.IsFalse(ok);
        Assert.AreEqual(Value, ops.RunKeyPath);
        Assert.IsFalse(ops.TaskPresent);
        Assert.AreEqual(0, ops.RemoveRunKeyCalls);
    }

    [TestMethod]
    public void Cancelled_task_removal_leaves_task_and_writes_no_run_key()
    {
        var (manager, ops) = Build();
        ops.TaskPresent = true;
        ops.RemoveTaskSucceeds = false;

        bool ok = manager.Sync(true, false);

        Assert.IsFalse(ok);
        Assert.IsTrue(ops.TaskPresent);
        Assert.IsNull(ops.RunKeyPath);
        Assert.AreEqual(0, ops.WriteRunKeyCalls);
    }

    [TestMethod]
    public void Re_syncing_with_elevation_does_not_recreate_the_task()
    {
        // Enabling a second CPU temp/power metric re-syncs while the task already exists. The
        // task must not be recreated, so no second UAC prompt is raised.
        var (manager, ops) = Build();
        ops.TaskPresent = true;

        bool ok = manager.Sync(true, true);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, ops.CreateTaskCalls);
        Assert.IsTrue(ops.TaskPresent);
    }

    [TestMethod]
    public void Re_enabling_same_run_key_writes_nothing()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = Value;

        manager.Sync(true, false);

        Assert.AreEqual(0, ops.WriteRunKeyCalls);
    }

    [TestMethod]
    public void RefreshRunKeyPath_rewrites_a_stale_path()
    {
        var (manager, ops) = Build();
        ops.RunKeyPath = "\"C:\\Old\\MiniMetrics.exe\"";

        manager.RefreshRunKeyPath();

        Assert.AreEqual(Value, ops.RunKeyPath);
    }

    [TestMethod]
    public void RefreshRunKeyPath_does_nothing_when_absent()
    {
        var (manager, ops) = Build();

        manager.RefreshRunKeyPath();

        Assert.IsNull(ops.RunKeyPath);
        Assert.AreEqual(0, ops.WriteRunKeyCalls);
    }

    [TestMethod]
    public void IsEnabled_true_when_task_present()
    {
        var (manager, ops) = Build();
        ops.TaskPresent = true;

        Assert.IsTrue(manager.IsEnabled());
    }

    [TestMethod]
    public void IsEnabled_false_when_neither_present()
    {
        var (manager, _) = Build();

        Assert.IsFalse(manager.IsEnabled());
    }
}
