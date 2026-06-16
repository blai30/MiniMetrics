using System.IO;
using MiniMetrics.Models;
using MiniMetrics.Services;
using Xunit;

namespace MiniMetrics.Tests;

public class SettingsStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");

    [Fact]
    public void Save_then_Load_round_trips_all_fields()
    {
        var path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings
        {
            X = 120,
            Y = 340,
            Locked = true,
            Hidden = false,
            AlwaysOnTop = true,
            Visibility = { ["gpu"] = false, ["cpu"] = true },
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(120, loaded.X);
        Assert.Equal(340, loaded.Y);
        Assert.True(loaded.Locked);
        Assert.False(loaded.Hidden);
        Assert.True(loaded.AlwaysOnTop);
        Assert.False(loaded.Visibility["gpu"]);
        Assert.True(loaded.Visibility["cpu"]);
    }

    [Fact]
    public void Save_then_Load_round_trips_appearance_fields()
    {
        var path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings
        {
            BackgroundColor = "#1A1F2B",
            Opacity = 73,
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal("#1A1F2B", loaded.BackgroundColor);
        Assert.Equal(73, loaded.Opacity);
    }

    [Fact]
    public void Save_then_Load_round_trips_snap_to_edges()
    {
        var path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings { SnapToEdges = false };

        store.Save(settings);
        var loaded = store.Load();

        Assert.False(loaded.SnapToEdges);
    }

    [Fact]
    public void Load_defaults_snap_to_edges_on_when_absent()
    {
        // A settings file written before the snap feature existed.
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"X\": 10, \"Y\": 20 }");

        var loaded = new SettingsStore(path).Load();

        Assert.True(loaded.SnapToEdges);
    }

    [Fact]
    public void Load_uses_appearance_defaults_when_absent()
    {
        // A settings file written before the appearance feature existed.
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"X\": 10, \"Y\": 20 }");

        var loaded = new SettingsStore(path).Load();

        Assert.Equal("#0F121D", loaded.BackgroundColor);
        Assert.Equal(96, loaded.Opacity);
    }

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var store = new SettingsStore(TempPath());
        var loaded = store.Load();

        Assert.Null(loaded.X);
        Assert.False(loaded.Locked);
        Assert.False(loaded.Hidden);
        Assert.Empty(loaded.Visibility);
    }

    [Fact]
    public void Load_returns_defaults_when_file_corrupt()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not valid json");

        var loaded = new SettingsStore(path).Load();

        Assert.Null(loaded.X);
        Assert.False(loaded.Locked);
    }

    [Fact]
    public void Save_then_Load_round_trips_datetime_fields()
    {
        var path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings
        {
            DateTimeX = 200,
            DateTimeY = 760,
            DateTimeHidden = false,
            TimeZoneId = "UTC",
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(200, loaded.DateTimeX);
        Assert.Equal(760, loaded.DateTimeY);
        Assert.False(loaded.DateTimeHidden);
        Assert.Equal("UTC", loaded.TimeZoneId);
    }

    [Fact]
    public void Save_then_Load_round_trips_gpu_widget_fields()
    {
        var path = TempPath();
        var store = new SettingsStore(path);
        var settings = new Settings
        {
            GpuX = 1234,
            GpuY = 567,
            GpuHidden = true,
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(1234, loaded.GpuX);
        Assert.Equal(567, loaded.GpuY);
        Assert.True(loaded.GpuHidden);
    }

    [Fact]
    public void Load_defaults_datetime_widget_hidden_when_absent()
    {
        // A settings file written before the datetime widget existed.
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"X\": 10, \"Y\": 20 }");

        var loaded = new SettingsStore(path).Load();

        Assert.True(loaded.DateTimeHidden);
        Assert.Null(loaded.DateTimeX);
        Assert.Null(loaded.TimeZoneId);
    }
}
