using System;
using System.IO;
using System.Threading.Tasks;
using MiniMetrics.Models;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class NotifyUpdateFlowTests
{
    private static NotifyUpdateFlow NewFlow(IReleaseSource source)
    {
        string path = Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");
        var controller = new SettingsController(new Settings(), new SettingsStore(path), new FakeSaveScheduler());
        var service = new UpdateService(source, new Version(1, 2, 0), controller, () => DateTimeOffset.UtcNow);
        return new NotifyUpdateFlow(service);
    }

    [TestMethod]
    public void Cannot_apply_in_app()
    {
        NotifyUpdateFlow flow = NewFlow(new FakeReleaseSource { Release = null });

        Assert.IsFalse(flow.CanApplyInApp);
    }

    [TestMethod]
    public async Task ApplyAndRestart_throws_because_a_loose_exe_cannot_self_replace()
    {
        NotifyUpdateFlow flow = NewFlow(new FakeReleaseSource { Release = null });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => flow.ApplyAndRestartAsync());
    }

    [TestMethod]
    public async Task Check_forwards_to_the_underlying_service()
    {
        var source = new FakeReleaseSource { Release = new ReleaseInfo("v1.3.0", "https://example/r") };
        NotifyUpdateFlow flow = NewFlow(source);

        UpdateCheckResult result = await flow.CheckAsync(manual: false);

        Assert.AreEqual(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.AreEqual("1.3.0", result.Version);
        Assert.AreEqual("https://example/r", result.ReleaseUrl);
    }
}
