using System;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class UpdateCheckDecisionTests
{
    private static UpdateCheckResult Evaluate(
        string latestTag, string? skippedVersion, bool manual) =>
        UpdateCheckDecision.Evaluate(
            new Version(1, 2, 0), latestTag, skippedVersion, manual, "https://example/r", "1.2.0");

    [TestMethod]
    public void Reports_an_available_update()
    {
        UpdateCheckResult result = Evaluate("v1.3.0", skippedVersion: null, manual: false);

        Assert.AreEqual(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.AreEqual("1.3.0", result.Version);
        Assert.AreEqual("https://example/r", result.ReleaseUrl);
    }

    [TestMethod]
    public void Reports_up_to_date_when_not_newer()
    {
        UpdateCheckResult result = Evaluate("v1.2.0", skippedVersion: null, manual: true);

        Assert.AreEqual(UpdateOutcome.UpToDate, result.Outcome);
        Assert.AreEqual("1.2.0", result.Version);
        Assert.IsNull(result.ReleaseUrl);
    }

    [TestMethod]
    public void Auto_check_suppresses_a_skipped_version()
    {
        UpdateCheckResult result = Evaluate("v1.3.0", skippedVersion: "1.3.0", manual: false);

        Assert.AreEqual(UpdateOutcome.UpToDate, result.Outcome);
    }

    [TestMethod]
    public void Manual_check_shows_a_skipped_version_anyway()
    {
        UpdateCheckResult result = Evaluate("v1.3.0", skippedVersion: "1.3.0", manual: true);

        Assert.AreEqual(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.AreEqual("1.3.0", result.Version);
    }

    [TestMethod]
    public void Unparseable_tag_is_treated_as_up_to_date()
    {
        UpdateCheckResult result = Evaluate("nightly-build", skippedVersion: null, manual: true);

        Assert.AreEqual(UpdateOutcome.UpToDate, result.Outcome);
        Assert.AreEqual("1.2.0", result.Version);
    }
}
