using System;
using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.Tests;

[TestClass]
public class UpdatePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void IsDue_when_never_checked()
    {
        Assert.IsTrue(UpdatePolicy.IsDue(null, UpdateCheckFrequency.Monthly, Now));
    }

    [TestMethod]
    public void IsDue_always_for_every_launch()
    {
        Assert.IsTrue(UpdatePolicy.IsDue(Now, UpdateCheckFrequency.EveryLaunch, Now));
    }

    [TestMethod]
    public void Daily_is_not_due_within_a_day()
    {
        Assert.IsFalse(UpdatePolicy.IsDue(Now.AddHours(-23), UpdateCheckFrequency.Daily, Now));
    }

    [TestMethod]
    public void Daily_is_due_after_a_day()
    {
        Assert.IsTrue(UpdatePolicy.IsDue(Now.AddHours(-25), UpdateCheckFrequency.Daily, Now));
    }

    [TestMethod]
    public void Weekly_is_due_after_seven_days()
    {
        Assert.IsTrue(UpdatePolicy.IsDue(Now.AddDays(-8), UpdateCheckFrequency.Weekly, Now));
        Assert.IsFalse(UpdatePolicy.IsDue(Now.AddDays(-6), UpdateCheckFrequency.Weekly, Now));
    }

    [TestMethod]
    public void Monthly_is_due_after_thirty_days()
    {
        Assert.IsTrue(UpdatePolicy.IsDue(Now.AddDays(-31), UpdateCheckFrequency.Monthly, Now));
        Assert.IsFalse(UpdatePolicy.IsDue(Now.AddDays(-29), UpdateCheckFrequency.Monthly, Now));
    }
}
