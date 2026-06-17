using System.IO;
using MiniMetrics.Models;
using MiniMetrics.Services;
using Xunit;

namespace MiniMetrics.Tests;

public class SettingsControllerTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");

    private static (SettingsController controller, SettingsStore store, FakeSaveScheduler scheduler) NewController(Settings? settings = null)
    {
        var store = new SettingsStore(TempPath());
        var scheduler = new FakeSaveScheduler();
        var controller = new SettingsController(settings ?? new Settings(), store, scheduler);
        return (controller, store, scheduler);
    }

    [Fact]
    public void Toggle_flips_value_and_persists_immediately()
    {
        var (controller, store, scheduler) = NewController();

        bool locked = controller.ToggleLocked();

        Assert.True(locked);
        Assert.True(controller.Current.Locked);
        Assert.True(store.Load().Locked);
        Assert.Equal(0, scheduler.ScheduleCount);
    }

    [Fact]
    public void Toggle_returns_new_value_on_each_call()
    {
        var (controller, _, _) = NewController();

        Assert.True(controller.ToggleCpuHidden());
        Assert.False(controller.ToggleCpuHidden());
    }

    [Fact]
    public void SetMetricVisibility_persists_immediately()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetMetricVisibility("cpu.temp", false);

        Assert.False(controller.Current.Visibility["cpu.temp"]);
        Assert.False(store.Load().Visibility["cpu.temp"]);
        Assert.Equal(0, scheduler.ScheduleCount);
    }

    [Fact]
    public void SetAppearance_updates_state_now_but_defers_the_write()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetAppearance("#112233", 50);

        // In-memory state is current immediately; the disk write waits for the debounce.
        Assert.Equal("#112233", controller.Current.BackgroundColor);
        Assert.Equal(50, controller.Current.Opacity);
        Assert.Equal(1, scheduler.ScheduleCount);
        Assert.Equal("#0F121D", store.Load().BackgroundColor);

        controller.Flush();

        Assert.Equal("#112233", store.Load().BackgroundColor);
        Assert.Equal(50, store.Load().Opacity);
    }

    [Fact]
    public void A_burst_of_debounced_edits_coalesces_into_one_write()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetCpuPosition(10, 10);
        controller.SetCpuPosition(20, 20);
        controller.SetCpuPosition(30, 30);

        // Scheduled three times, but nothing is written until the single flush runs.
        Assert.Equal(3, scheduler.ScheduleCount);
        Assert.Null(store.Load().X);

        controller.Flush();

        Assert.Equal(1, scheduler.FlushCount);
        Assert.Equal(30, store.Load().X);
        Assert.Equal(30, store.Load().Y);
    }

    [Fact]
    public void SetTimeZone_defers_the_write()
    {
        var (controller, store, _) = NewController();

        controller.SetTimeZone("UTC");

        Assert.Equal("UTC", controller.Current.TimeZoneId);
        Assert.Null(store.Load().TimeZoneId);

        controller.Flush();

        Assert.Equal("UTC", store.Load().TimeZoneId);
    }

    [Fact]
    public void Flush_with_nothing_pending_does_not_write()
    {
        var (controller, store, _) = NewController();

        controller.Flush();

        // No save was ever scheduled, so the file never appears and Load falls back to defaults.
        Assert.Null(store.Load().X);
    }

    [Fact]
    public void Constructor_migrates_legacy_whole_card_visibility_keys()
    {
        var settings = new Settings { Visibility = { ["cpu"] = false, ["gpu"] = true } };

        var (controller, _, _) = NewController(settings);

        Settings current = controller.Current;
        Assert.False(current.Visibility.ContainsKey("cpu"));
        Assert.False(current.Visibility["cpu.usage"]);
        Assert.False(current.Visibility["cpu.temp"]);
        Assert.False(current.Visibility["cpu.power"]);
        Assert.True(current.Visibility["gpu.usage"]);
    }
}
