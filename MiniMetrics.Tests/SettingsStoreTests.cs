using MiniMetrics.Models;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class SettingsStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");

    [TestMethod]
    public void Save_then_Load_round_trips_all_fields()
    {
        string path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings
        {
            X = 120,
            Y = 340,
            Locked = true,
            Hidden = false,
            AlwaysOnTop = true,
            Visibility = { ["gpu"] = false, ["cpu"] = true }
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.AreEqual(120, loaded.X);
        Assert.AreEqual(340, loaded.Y);
        Assert.IsTrue(loaded.Locked);
        Assert.IsFalse(loaded.Hidden);
        Assert.IsTrue(loaded.AlwaysOnTop);
        Assert.IsFalse(loaded.Visibility["gpu"]);
        Assert.IsTrue(loaded.Visibility["cpu"]);
    }

    [TestMethod]
    public void Save_then_Load_round_trips_appearance_fields()
    {
        string path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings
        {
            BackgroundColor = "#1A1F2B",
            Opacity = 73
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.AreEqual("#1A1F2B", loaded.BackgroundColor);
        Assert.AreEqual(73, loaded.Opacity);
    }

    [TestMethod]
    public void Save_then_Load_round_trips_snap_to_edges()
    {
        string path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings { SnapToEdges = false };

        store.Save(settings);
        var loaded = store.Load();

        Assert.IsFalse(loaded.SnapToEdges);
    }

    [TestMethod]
    public void Load_defaults_snap_to_edges_on_when_absent()
    {
        // A settings file written before the snap feature existed.
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"X\": 10, \"Y\": 20 }");

        var loaded = new SettingsStore(path).Load();

        Assert.IsTrue(loaded.SnapToEdges);
    }

    [TestMethod]
    public void Load_uses_appearance_defaults_when_absent()
    {
        // A settings file written before the appearance feature existed.
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"X\": 10, \"Y\": 20 }");

        var loaded = new SettingsStore(path).Load();

        Assert.AreEqual("#0F121D", loaded.BackgroundColor);
        Assert.AreEqual(96, loaded.Opacity);
    }

    [TestMethod]
    public void Load_returns_defaults_when_file_missing()
    {
        var store = new SettingsStore(TempPath());
        var loaded = store.Load();

        Assert.IsNull(loaded.X);
        Assert.IsFalse(loaded.Locked);
        Assert.IsFalse(loaded.Hidden);
        Assert.IsEmpty(loaded.Visibility);
    }

    [TestMethod]
    public void Load_returns_defaults_when_file_corrupt()
    {
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not valid json");

        var loaded = new SettingsStore(path).Load();

        Assert.IsNull(loaded.X);
        Assert.IsFalse(loaded.Locked);
    }

    [TestMethod]
    public void Load_backs_up_a_corrupt_file_before_resetting()
    {
        // A corrupt file is preserved as .bak so the user's lost config is recoverable and diagnosable.
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not valid json");

        new SettingsStore(path).Load();

        Assert.IsTrue(File.Exists(path + ".bak"));
        Assert.AreEqual("{ this is not valid json", File.ReadAllText(path + ".bak"));
    }

    [TestMethod]
    public void Save_then_Load_round_trips_datetime_fields()
    {
        string path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings
        {
            DateTimeX = 200,
            DateTimeY = 760,
            DateTimeHidden = false,
            TimeZoneId = "UTC"
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.AreEqual(200, loaded.DateTimeX);
        Assert.AreEqual(760, loaded.DateTimeY);
        Assert.IsFalse(loaded.DateTimeHidden);
        Assert.AreEqual("UTC", loaded.TimeZoneId);
    }

    [TestMethod]
    public void Save_then_Load_round_trips_gpu_widget_fields()
    {
        string path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings
        {
            GpuX = 1234,
            GpuY = 567,
            GpuHidden = true
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.AreEqual(1234, loaded.GpuX);
        Assert.AreEqual(567, loaded.GpuY);
        Assert.IsTrue(loaded.GpuHidden);
    }

    [TestMethod]
    public void Load_defaults_datetime_widget_hidden_when_absent()
    {
        // A settings file written before the datetime widget existed.
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"X\": 10, \"Y\": 20 }");

        var loaded = new SettingsStore(path).Load();

        Assert.IsTrue(loaded.DateTimeHidden);
        Assert.IsNull(loaded.DateTimeX);
        Assert.IsNull(loaded.TimeZoneId);
    }

    [TestMethod]
    public void New_settings_default_update_fields()
    {
        var settings = new Settings();

        Assert.IsTrue(settings.UpdateCheckEnabled);
        Assert.AreEqual(UpdateCheckFrequency.Daily, settings.UpdateFrequency);
        Assert.IsNull(settings.LastUpdateCheckUtc);
        Assert.IsNull(settings.SkippedUpdateVersion);
    }

    [TestMethod]
    public void Update_fields_round_trip_through_save_and_load()
    {
        string path = Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");
        var store = new SettingsStore(path);
        var when = new DateTimeOffset(2026, 6, 17, 8, 0, 0, TimeSpan.Zero);

        store.Save(new()
        {
            UpdateCheckEnabled = false,
            UpdateFrequency = UpdateCheckFrequency.Weekly,
            LastUpdateCheckUtc = when,
            SkippedUpdateVersion = "1.3.0"
        });

        var loaded = store.Load();

        Assert.IsFalse(loaded.UpdateCheckEnabled);
        Assert.AreEqual(UpdateCheckFrequency.Weekly, loaded.UpdateFrequency);
        Assert.AreEqual(when, loaded.LastUpdateCheckUtc);
        Assert.AreEqual("1.3.0", loaded.SkippedUpdateVersion);
    }

    [TestMethod]
    public void Save_then_Load_round_trips_clock_format_and_locale_fields()
    {
        string path = TempPath();
        var store = new SettingsStore(path);
        store.Save(new()
        {
            ClockLocaleId = "fr-FR",
            ClockTimeFormat = "HH:mm",
            ClockDateFormat = "yyyy-MM-dd",
            ClockTimeFormatHover = "HH:mm:ss",
            ClockDateFormatHover = "u"
        });

        var loaded = store.Load();

        Assert.AreEqual("fr-FR", loaded.ClockLocaleId);
        Assert.AreEqual("HH:mm", loaded.ClockTimeFormat);
        Assert.AreEqual("yyyy-MM-dd", loaded.ClockDateFormat);
        Assert.AreEqual("HH:mm:ss", loaded.ClockTimeFormatHover);
        Assert.AreEqual("u", loaded.ClockDateFormatHover);
    }

    [TestMethod]
    public void Load_defaults_clock_format_and_locale_to_null_when_absent()
    {
        // A settings file written before the custom-format feature existed.
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"X\": 10, \"Y\": 20 }");

        var loaded = new SettingsStore(path).Load();

        Assert.IsNull(loaded.ClockLocaleId);
        Assert.IsNull(loaded.ClockTimeFormat);
        Assert.IsNull(loaded.ClockDateFormat);
        Assert.IsNull(loaded.ClockTimeFormatHover);
        Assert.IsNull(loaded.ClockDateFormatHover);
    }
}
