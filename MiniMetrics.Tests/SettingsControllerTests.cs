using MiniMetrics.Models;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class SettingsControllerTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");

    private static (SettingsController controller, SettingsStore store, FakeSaveScheduler scheduler) NewController(
        Settings? settings = null)
    {
        var store = new SettingsStore(TempPath());
        var scheduler = new FakeSaveScheduler();
        var controller = new SettingsController(settings ?? new Settings(), store, scheduler);
        return (controller, store, scheduler);
    }

    [TestMethod]
    public void Toggle_flips_value_and_persists_immediately()
    {
        var (controller, store, scheduler) = NewController();

        bool locked = controller.ToggleLocked();

        Assert.IsTrue(locked);
        Assert.IsTrue(controller.Current.Locked);
        Assert.IsTrue(store.Load().Locked);
        Assert.AreEqual(0, scheduler.ScheduleCount);
    }

    [TestMethod]
    public void Toggle_returns_new_value_on_each_call()
    {
        var (controller, _, _) = NewController();

        Assert.IsTrue(controller.ToggleCpuHidden());
        Assert.IsFalse(controller.ToggleCpuHidden());
    }

    [TestMethod]
    public void SetMetricVisibility_persists_immediately()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetMetricVisibility("cpu.temp", false);

        Assert.IsFalse(controller.Current.Visibility["cpu.temp"]);
        Assert.IsFalse(store.Load().Visibility["cpu.temp"]);
        Assert.AreEqual(0, scheduler.ScheduleCount);
    }

    [TestMethod]
    public void SetAppearance_writes_the_dark_color_and_defers_the_write()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetAppearance(true, "#112233", 50);

        // In-memory state is current immediately; the disk write waits for the debounce.
        Assert.AreEqual("#112233", controller.Current.BackgroundColor);
        Assert.AreEqual(50, controller.Current.Opacity);
        Assert.AreEqual(1, scheduler.ScheduleCount);
        Assert.AreEqual("#0F121D", store.Load().BackgroundColor);

        controller.Flush();

        Assert.AreEqual("#112233", store.Load().BackgroundColor);
        Assert.AreEqual(50, store.Load().Opacity);
    }

    [TestMethod]
    public void SetAppearance_writes_the_light_color_when_targeting_light()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetAppearance(false, "#FAFBFF", 80);
        controller.Flush();

        Assert.AreEqual("#FAFBFF", store.Load().LightBackgroundColor);
        Assert.AreEqual("#0F121D", store.Load().BackgroundColor);
        Assert.AreEqual(80, store.Load().Opacity);
    }

    [TestMethod]
    public void SetTheme_persists_immediately()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetTheme(AppTheme.Light);

        Assert.AreEqual(AppTheme.Light, controller.Current.Theme);
        Assert.AreEqual(AppTheme.Light, store.Load().Theme);
        Assert.AreEqual(0, scheduler.ScheduleCount);
    }

    [TestMethod]
    public void SetCpuCompact_persists_immediately()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetCpuCompact(true);

        Assert.IsTrue(controller.Current.CpuCompact);
        Assert.IsTrue(store.Load().CpuCompact);
        Assert.AreEqual(0, scheduler.ScheduleCount);
    }

    [TestMethod]
    public void SetGpuCompact_persists_immediately()
    {
        var (controller, store, _) = NewController();

        controller.SetGpuCompact(true);

        Assert.IsTrue(controller.Current.GpuCompact);
        Assert.IsTrue(store.Load().GpuCompact);
    }

    [TestMethod]
    public void SetDateTimeCompact_persists_immediately()
    {
        var (controller, store, _) = NewController();

        controller.SetDateTimeCompact(true);

        Assert.IsTrue(controller.Current.DateTimeCompact);
        Assert.IsTrue(store.Load().DateTimeCompact);
    }

    [TestMethod]
    public void Compact_flags_default_to_false()
    {
        var (controller, _, _) = NewController();

        Assert.IsFalse(controller.Current.CpuCompact);
        Assert.IsFalse(controller.Current.GpuCompact);
        Assert.IsFalse(controller.Current.DateTimeCompact);
    }

    [TestMethod]
    public void ClockAlignment_defaults_to_Left()
    {
        var (controller, _, _) = NewController();

        Assert.AreEqual(ClockAlignment.Left, controller.Current.ClockAlignment);
    }

    [TestMethod]
    public void SetClockAlignment_persists_immediately()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetClockAlignment(ClockAlignment.Right);

        Assert.AreEqual(ClockAlignment.Right, controller.Current.ClockAlignment);
        Assert.AreEqual(ClockAlignment.Right, store.Load().ClockAlignment);
        Assert.AreEqual(0, scheduler.ScheduleCount);
    }

    [TestMethod]
    public void A_burst_of_debounced_edits_coalesces_into_one_write()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetCpuPosition(10, 10);
        controller.SetCpuPosition(20, 20);
        controller.SetCpuPosition(30, 30);

        // Scheduled three times, but nothing is written until the single flush runs.
        Assert.AreEqual(3, scheduler.ScheduleCount);
        Assert.IsNull(store.Load().X);

        controller.Flush();

        Assert.AreEqual(1, scheduler.FlushCount);
        Assert.AreEqual(30, store.Load().X);
        Assert.AreEqual(30, store.Load().Y);
    }

    [TestMethod]
    public void SetTimeZone_defers_the_write()
    {
        var (controller, store, _) = NewController();

        controller.SetTimeZone("UTC");

        Assert.AreEqual("UTC", controller.Current.TimeZoneId);
        Assert.IsNull(store.Load().TimeZoneId);

        controller.Flush();

        Assert.AreEqual("UTC", store.Load().TimeZoneId);
    }

    [TestMethod]
    public void Flush_with_nothing_pending_does_not_write()
    {
        var (controller, store, _) = NewController();

        controller.Flush();

        // No save was ever scheduled, so the file never appears and Load falls back to defaults.
        Assert.IsNull(store.Load().X);
    }

    [TestMethod]
    public void Constructor_migrates_legacy_whole_card_visibility_keys()
    {
        var settings = new Settings { Visibility = { ["cpu"] = false, ["gpu"] = true } };

        var (controller, _, _) = NewController(settings);

        var current = controller.Current;
        Assert.IsFalse(current.Visibility.ContainsKey("cpu"));
        Assert.IsFalse(current.Visibility["cpu.usage"]);
        Assert.IsFalse(current.Visibility["cpu.temp"]);
        Assert.IsFalse(current.Visibility["cpu.power"]);
        Assert.IsTrue(current.Visibility["gpu.usage"]);
    }

    [TestMethod]
    public void Constructor_seeds_cpu_temp_and_power_off_by_default()
    {
        var (controller, _, _) = NewController();

        Assert.IsFalse(controller.Current.Visibility["cpu.temp"]);
        Assert.IsFalse(controller.Current.Visibility["cpu.power"]);
    }

    [TestMethod]
    public void Constructor_keeps_an_explicit_elevation_visibility_value()
    {
        var settings = new Settings { Visibility = { ["cpu.temp"] = true } };

        var (controller, _, _) = NewController(settings);

        Assert.IsTrue(controller.Current.Visibility["cpu.temp"]);
        Assert.IsFalse(controller.Current.Visibility["cpu.power"]);
    }

    [TestMethod]
    public void SetUpdatePreferences_updates_state_now_but_defers_the_write()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetUpdatePreferences(false, UpdateCheckFrequency.Monthly);

        Assert.IsFalse(controller.Current.UpdateCheckEnabled);
        Assert.AreEqual(UpdateCheckFrequency.Monthly, controller.Current.UpdateFrequency);
        Assert.AreEqual(1, scheduler.ScheduleCount);
        Assert.IsTrue(store.Load().UpdateCheckEnabled); // default until flush

        controller.Flush();

        Assert.IsFalse(store.Load().UpdateCheckEnabled);
        Assert.AreEqual(UpdateCheckFrequency.Monthly, store.Load().UpdateFrequency);
    }

    [TestMethod]
    public void SetLastUpdateCheck_persists_immediately()
    {
        var (controller, store, scheduler) = NewController();
        var when = new DateTimeOffset(2026, 6, 17, 8, 0, 0, TimeSpan.Zero);

        controller.SetLastUpdateCheck(when);

        Assert.AreEqual(when, controller.Current.LastUpdateCheckUtc);
        Assert.AreEqual(when, store.Load().LastUpdateCheckUtc);
        Assert.AreEqual(0, scheduler.ScheduleCount);
    }

    [TestMethod]
    public void SetSkippedUpdateVersion_persists_immediately()
    {
        var (controller, store, _) = NewController();

        controller.SetSkippedUpdateVersion("1.4.0");

        Assert.AreEqual("1.4.0", controller.Current.SkippedUpdateVersion);
        Assert.AreEqual("1.4.0", store.Load().SkippedUpdateVersion);
    }

    [TestMethod]
    public void SetClockFormats_updates_state_now_but_defers_the_write()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetClockFormats("HH:mm", "yyyy-MM-dd", "HH:mm:ss", "u");

        Assert.AreEqual("HH:mm", controller.Current.ClockTimeFormat);
        Assert.AreEqual(1, scheduler.ScheduleCount);
        Assert.IsNull(store.Load().ClockTimeFormat);

        controller.Flush();

        Assert.AreEqual("HH:mm", store.Load().ClockTimeFormat);
        Assert.AreEqual("yyyy-MM-dd", store.Load().ClockDateFormat);
        Assert.AreEqual("HH:mm:ss", store.Load().ClockTimeFormatHover);
        Assert.AreEqual("u", store.Load().ClockDateFormatHover);
    }

    [TestMethod]
    public void SetClockLocale_updates_state_now_but_defers_the_write()
    {
        var (controller, store, _) = NewController();

        controller.SetClockLocale("fr-FR");

        Assert.AreEqual("fr-FR", controller.Current.ClockLocaleId);
        Assert.IsNull(store.Load().ClockLocaleId);

        controller.Flush();

        Assert.AreEqual("fr-FR", store.Load().ClockLocaleId);
    }

    [TestMethod]
    public void SetWidgetFont_updates_state_now_but_defers_the_write()
    {
        var (controller, store, scheduler) = NewController();

        controller.SetWidgetFont("Cascadia Code", 125, WidgetFontWeight.Bold);

        Assert.AreEqual("Cascadia Code", controller.Current.WidgetFontFamily);
        Assert.AreEqual(125, controller.Current.WidgetFontScale);
        Assert.AreEqual(WidgetFontWeight.Bold, controller.Current.WidgetFontWeight);
        Assert.AreEqual(1, scheduler.ScheduleCount);
        Assert.IsNull(store.Load().WidgetFontFamily); // default until flush

        controller.Flush();

        Assert.AreEqual("Cascadia Code", store.Load().WidgetFontFamily);
        Assert.AreEqual(125, store.Load().WidgetFontScale);
        Assert.AreEqual(WidgetFontWeight.Bold, store.Load().WidgetFontWeight);
    }

    [TestMethod]
    public void Widget_font_defaults_are_inter_regular_full_size()
    {
        var (controller, _, _) = NewController();

        Assert.IsNull(controller.Current.WidgetFontFamily);
        Assert.AreEqual(100, controller.Current.WidgetFontScale);
        Assert.AreEqual(WidgetFontWeight.Regular, controller.Current.WidgetFontWeight);
    }
}
