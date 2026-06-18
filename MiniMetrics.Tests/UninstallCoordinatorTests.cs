using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class UninstallCoordinatorTests
{
    [TestMethod]
    public void Declined_task_removal_aborts_and_touches_nothing_else()
    {
        var ops = new FakeStartupOperations { TaskPresent = true, RemoveTaskSucceeds = false, RunKeyPath = "x" };
        int launches = 0;
        var coordinator = new UninstallCoordinator(ops, () => launches++);

        UninstallOutcome outcome = coordinator.Run();

        Assert.AreEqual(UninstallOutcome.Aborted, outcome);
        Assert.IsTrue(ops.TaskPresent);
        Assert.AreEqual(0, ops.RemoveRunKeyCalls);
        Assert.AreEqual("x", ops.RunKeyPath);
        Assert.AreEqual(0, launches);
    }

    [TestMethod]
    public void Successful_task_removal_clears_run_key_and_launches_uninstaller()
    {
        var ops = new FakeStartupOperations { TaskPresent = true, RemoveTaskSucceeds = true, RunKeyPath = "x" };
        int launches = 0;
        var coordinator = new UninstallCoordinator(ops, () => launches++);

        UninstallOutcome outcome = coordinator.Run();

        Assert.AreEqual(UninstallOutcome.Completed, outcome);
        Assert.IsFalse(ops.TaskPresent);
        Assert.AreEqual(1, ops.RemoveRunKeyCalls);
        Assert.AreEqual(1, launches);
    }

    [TestMethod]
    public void No_task_clears_run_key_and_launches_without_a_prompt()
    {
        var ops = new FakeStartupOperations { TaskPresent = false, RunKeyPath = "x" };
        int launches = 0;
        var coordinator = new UninstallCoordinator(ops, () => launches++);

        UninstallOutcome outcome = coordinator.Run();

        Assert.AreEqual(UninstallOutcome.Completed, outcome);
        Assert.AreEqual(0, ops.RemoveTaskCalls);
        Assert.AreEqual(1, ops.RemoveRunKeyCalls);
        Assert.AreEqual(1, launches);
    }
}
