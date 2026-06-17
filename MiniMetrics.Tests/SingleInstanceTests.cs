using MiniMetrics.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class SingleInstanceTests
{
    // Each test uses its own mutex name so the guard is isolated from the live app and from other tests.
    [TestMethod]
    public void First_acquirer_is_the_only_instance()
    {
        using var first = SingleInstance.Acquire(@"Local\MiniMetrics.Test.First");

        Assert.IsTrue(first.IsOnlyInstance);
    }

    [TestMethod]
    public void Second_acquirer_is_not_the_only_instance()
    {
        using var first = SingleInstance.Acquire(@"Local\MiniMetrics.Test.Second");
        using var second = SingleInstance.Acquire(@"Local\MiniMetrics.Test.Second");

        Assert.IsTrue(first.IsOnlyInstance);
        Assert.IsFalse(second.IsOnlyInstance);
    }

    [TestMethod]
    public void Instance_is_available_again_after_the_owner_is_disposed()
    {
        using (var first = SingleInstance.Acquire(@"Local\MiniMetrics.Test.Reacquire"))
        {
            Assert.IsTrue(first.IsOnlyInstance);
        }

        using var second = SingleInstance.Acquire(@"Local\MiniMetrics.Test.Reacquire");

        Assert.IsTrue(second.IsOnlyInstance);
    }
}
