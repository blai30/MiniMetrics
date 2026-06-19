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

    [TestMethod]
    public void Evaluate_flags_a_newer_release()
    {
        var decision = UpdatePolicy.Evaluate(new(1, 2, 0), "v1.3.0", null);

        Assert.IsTrue(decision.UpdateAvailable);
        Assert.IsTrue(decision.ShouldNotify);
        Assert.AreEqual(new(1, 3, 0), decision.LatestVersion);
    }

    [TestMethod]
    public void Evaluate_ignores_an_older_or_equal_release()
    {
        Assert.IsFalse(UpdatePolicy.Evaluate(new(1, 3, 0), "v1.2.0", null).UpdateAvailable);
        Assert.IsFalse(UpdatePolicy.Evaluate(new(1, 3, 0), "1.3.0", null).UpdateAvailable);
    }

    [TestMethod]
    public void Evaluate_strips_leading_v_case_insensitively()
    {
        Assert.IsTrue(UpdatePolicy.Evaluate(new(1, 2, 0), "V1.3.0", null).UpdateAvailable);
    }

    [TestMethod]
    public void Evaluate_suppresses_notify_for_the_skipped_version()
    {
        var decision = UpdatePolicy.Evaluate(new(1, 2, 0), "v1.3.0", "1.3.0");

        Assert.IsTrue(decision.UpdateAvailable);
        Assert.IsFalse(decision.ShouldNotify);
    }

    [TestMethod]
    public void Evaluate_still_notifies_for_a_version_above_the_skipped_one()
    {
        var decision = UpdatePolicy.Evaluate(new(1, 2, 0), "v1.4.0", "1.3.0");

        Assert.IsTrue(decision.ShouldNotify);
    }

    [TestMethod]
    public void Evaluate_treats_an_unparseable_tag_as_no_update()
    {
        var decision = UpdatePolicy.Evaluate(new(1, 2, 0), "nightly-build", null);

        Assert.IsFalse(decision.UpdateAvailable);
        Assert.IsNull(decision.LatestVersion);
    }
}
