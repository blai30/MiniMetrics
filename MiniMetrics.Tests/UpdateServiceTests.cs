using MiniMetrics.Models;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class UpdateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private static (UpdateService service, SettingsController controller) NewService(
        IReleaseSource source, Settings? settings = null)
    {
        string path = Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");
        var controller =
            new SettingsController(settings ?? new Settings(), new SettingsStore(path), new FakeSaveScheduler());
        var service = new UpdateService(source, new Version(1, 2, 0), controller, () => Now);
        return (service, controller);
    }

    [TestMethod]
    public async Task Reports_an_available_update_and_stamps_the_check()
    {
        var source = new FakeReleaseSource { Release = new ReleaseInfo("v1.3.0", "https://example/r") };
        var (service, controller) = NewService(source);

        var result = await service.CheckAsync(false);

        Assert.AreEqual(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.AreEqual("1.3.0", result.Version);
        Assert.AreEqual("https://example/r", result.ReleaseUrl);
        Assert.AreEqual(Now, controller.Current.LastUpdateCheckUtc);
    }

    [TestMethod]
    public async Task Reports_up_to_date_when_not_newer()
    {
        var source = new FakeReleaseSource { Release = new ReleaseInfo("v1.0.0", "https://example/r") };
        var (service, controller) = NewService(source);

        var result = await service.CheckAsync(true);

        Assert.AreEqual(UpdateOutcome.UpToDate, result.Outcome);
        Assert.AreEqual("1.2.0", result.Version);
        Assert.AreEqual(Now, controller.Current.LastUpdateCheckUtc);
    }

    [TestMethod]
    public async Task Reports_failure_and_leaves_the_timestamp_untouched()
    {
        var source = new FakeReleaseSource { Release = null };
        var (service, controller) = NewService(source);

        var result = await service.CheckAsync(true);

        Assert.AreEqual(UpdateOutcome.Failed, result.Outcome);
        Assert.IsNull(controller.Current.LastUpdateCheckUtc);
    }

    [TestMethod]
    public async Task Auto_check_suppresses_a_skipped_version()
    {
        var source = new FakeReleaseSource { Release = new ReleaseInfo("v1.3.0", "https://example/r") };
        var (service, _) = NewService(source, new Settings { SkippedUpdateVersion = "1.3.0" });

        var result = await service.CheckAsync(false);

        Assert.AreEqual(UpdateOutcome.UpToDate, result.Outcome);
    }

    [TestMethod]
    public async Task Manual_check_shows_a_skipped_version_anyway()
    {
        var source = new FakeReleaseSource { Release = new ReleaseInfo("v1.3.0", "https://example/r") };
        var (service, _) = NewService(source, new Settings { SkippedUpdateVersion = "1.3.0" });

        var result = await service.CheckAsync(true);

        Assert.AreEqual(UpdateOutcome.UpdateAvailable, result.Outcome);
    }

    [TestMethod]
    public async Task A_second_check_while_one_is_in_flight_is_busy()
    {
        var gated = new GatedReleaseSource();
        var (service, _) = NewService(gated);

        var first = service.CheckAsync(false);
        var second = await service.CheckAsync(true);

        Assert.AreEqual(UpdateOutcome.Busy, second.Outcome);

        gated.Gate.SetResult(null);
        await first;
    }
}
