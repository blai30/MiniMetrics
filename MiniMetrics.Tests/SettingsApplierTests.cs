using System.Globalization;
using MiniMetrics.Lib;
using MiniMetrics.Models;
using MiniMetrics.Services;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Tests;

[TestClass]
public class SettingsApplierTests
{
    // Records the appearance and style fan-out so the tests can assert a change reached every widget
    // through the shared seam rather than inspecting derived brushes.
    private sealed class RecordingWidgetDisplay : IWidgetDisplay
    {
        public int AppearanceCount { get; private set; }
        public string? LastBackground { get; private set; }
        public int LastOpacity { get; private set; }
        public int StyleCount { get; private set; }
        public WidgetStyleProfile? LastProfile { get; private set; }

        public void ApplyAppearance(string backgroundColor, int opacity)
        {
            AppearanceCount++;
            LastBackground = backgroundColor;
            LastOpacity = opacity;
        }

        public void ApplyStyle(WidgetStyleProfile profile)
        {
            StyleCount++;
            LastProfile = profile;
        }
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");

    private sealed class Harness
    {
        public SettingsApplier Applier { get; set; } = null!;
        public required SettingsController Controller { get; init; }
        public required MetricWidgetViewModel Cpu { get; init; }
        public required MetricWidgetViewModel Gpu { get; init; }
        public required DateTimeWidgetViewModel DateTime { get; init; }
        public required RecordingWidgetDisplay Widget { get; init; }
        public int ThemeVariantApplied { get; set; }
        public bool IsDark { get; set; } = true;
    }

    private static Harness NewHarness(Settings? settings = null)
    {
        var controller = new SettingsController(
            settings ?? new Settings(), new(TempPath()), new FakeSaveScheduler());
        var widget = new RecordingWidgetDisplay();

        var harness = new Harness
        {
            Controller = controller,
            Cpu = new("cpu", "ram"),
            Gpu = new("gpu", "vram"),
            DateTime = new(),
            Widget = widget
        };

        // The delegates close over this same harness so a test can flip IsDark and read ThemeVariantApplied.
        harness.Applier = new(
            controller, harness.Cpu, harness.Gpu, harness.DateTime, [widget],
            () => harness.ThemeVariantApplied++,
            () => harness.IsDark);

        return harness;
    }

    [TestMethod]
    public void Compact_persists_and_reflects_onto_the_named_widget()
    {
        var harness = NewHarness();

        harness.Applier.Apply(new SettingChange.Compact("cpu", true));

        Assert.IsTrue(harness.Controller.Current.CpuCompact);
        Assert.IsTrue(harness.Cpu.IsCompact);
        Assert.IsFalse(harness.Gpu.IsCompact);
        Assert.IsFalse(harness.DateTime.IsCompact);
    }

    [TestMethod]
    public void Compact_clock_targets_the_date_time_widget()
    {
        var harness = NewHarness();

        harness.Applier.Apply(new SettingChange.Compact("clock", true));

        Assert.IsTrue(harness.Controller.Current.DateTimeCompact);
        Assert.IsTrue(harness.DateTime.IsCompact);
        Assert.IsFalse(harness.Cpu.IsCompact);
    }

    [TestMethod]
    public void Appearance_writes_the_variant_color_and_fans_out_to_widgets()
    {
        var harness = NewHarness();

        harness.Applier.Apply(new SettingChange.Appearance(true, "#112233", 40));

        Assert.AreEqual("#112233", harness.Controller.Current.BackgroundColor);
        Assert.AreEqual(40, harness.Controller.Current.Opacity);
        Assert.AreEqual(1, harness.Widget.AppearanceCount);
        Assert.AreEqual(40, harness.Widget.LastOpacity);
    }

    [TestMethod]
    public void Theme_persists_requests_the_variant_and_refreshes()
    {
        var harness = NewHarness();

        harness.Applier.Apply(new SettingChange.Theme(AppTheme.Light));

        Assert.AreEqual(AppTheme.Light, harness.Controller.Current.Theme);
        Assert.AreEqual(1, harness.ThemeVariantApplied);
        Assert.AreEqual(1, harness.Widget.AppearanceCount);
    }

    [TestMethod]
    public void WidgetStyle_persists_and_pushes_the_profile_to_widgets()
    {
        var harness = NewHarness();

        harness.Applier.Apply(new SettingChange.WidgetStyle("Inter", 150, WidgetFontWeight.Bold));

        Assert.AreEqual(150, harness.Controller.Current.WidgetScale);
        Assert.AreEqual(WidgetFontWeight.Bold, harness.Controller.Current.WidgetFontWeight);
        Assert.AreEqual(1, harness.Widget.StyleCount);
        Assert.AreEqual(1.5, harness.Widget.LastProfile!.Value.Scale);
    }

    [TestMethod]
    public void TimeZone_persists_the_id_and_reformats_the_clock()
    {
        var harness = NewHarness();
        harness.DateTime.SetLocale(CultureInfo.GetCultureInfo("en-US"));
        harness.DateTime.Tick(new(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        harness.Applier.Apply(new SettingChange.TimeZone("UTC"));

        Assert.AreEqual("UTC", harness.Controller.Current.TimeZoneId);
        Assert.AreEqual("12:00:00 PM", harness.DateTime.TimeText);
    }

    [TestMethod]
    public void ClockLocale_persists_the_name_and_reformats_the_clock()
    {
        var harness = NewHarness();
        harness.DateTime.SetTimeZone(TimeZoneInfo.Utc);
        harness.DateTime.Tick(new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero));

        harness.Applier.Apply(new SettingChange.ClockLocale(CultureInfo.GetCultureInfo("en-US")));

        Assert.AreEqual("en-US", harness.Controller.Current.ClockLocaleId);
        Assert.AreEqual("2:26:42 PM", harness.DateTime.TimeText);
    }

    [TestMethod]
    public void Alignment_persists_and_realigns_the_clock()
    {
        var harness = NewHarness();

        harness.Applier.Apply(new SettingChange.Alignment(ClockAlignment.Center));

        Assert.AreEqual(ClockAlignment.Center, harness.Controller.Current.ClockAlignment);
        Assert.AreEqual(Avalonia.Media.TextAlignment.Center, harness.DateTime.TextAlignment);
    }

    [TestMethod]
    public void UpdatePreferences_persists_without_touching_widgets()
    {
        var harness = NewHarness();

        harness.Applier.Apply(new SettingChange.UpdatePreferences(false, UpdateCheckFrequency.Weekly));

        Assert.IsFalse(harness.Controller.Current.UpdateCheckEnabled);
        Assert.AreEqual(UpdateCheckFrequency.Weekly, harness.Controller.Current.UpdateFrequency);
        Assert.AreEqual(0, harness.Widget.AppearanceCount);
        Assert.AreEqual(0, harness.Widget.StyleCount);
    }

    [TestMethod]
    public void ApplyAppearance_uses_the_light_color_when_resolved_light()
    {
        var settings = new Settings { BackgroundColor = "#000000", LightBackgroundColor = "#FFFFFF" };
        var harness = NewHarness(settings);
        harness.IsDark = false;

        harness.Applier.ApplyAppearance();

        Assert.AreEqual("#FFFFFF", harness.Widget.LastBackground);
    }
}
