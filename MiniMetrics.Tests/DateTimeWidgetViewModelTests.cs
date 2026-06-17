using System;
using MiniMetrics.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class DateTimeWidgetViewModelTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero);

    [TestMethod]
    public void Tick_formats_time_and_date_in_12_hour_by_default()
    {
        var vm = new DateTimeWidgetViewModel();
        vm.SetTimeZone(TimeZoneInfo.Utc);

        vm.Tick(Instant);

        Assert.AreEqual("2:26:42 PM", vm.TimeText);
        Assert.AreEqual("Tuesday, June 16, 2026", vm.DateText);
    }

    [TestMethod]
    public void Toggling_Is24Hour_reformats_immediately_without_a_new_tick()
    {
        var vm = new DateTimeWidgetViewModel();
        vm.SetTimeZone(TimeZoneInfo.Utc);
        vm.Tick(Instant);

        vm.Is24Hour = true;

        Assert.AreEqual("14:26:42", vm.TimeText);
    }

    [TestMethod]
    public void SetTimeZone_reformats_the_last_instant()
    {
        var vm = new DateTimeWidgetViewModel();
        vm.SetTimeZone(TimeZoneInfo.Utc);
        vm.Tick(Instant);

        vm.SetTimeZone(TimeZoneInfo.CreateCustomTimeZone("m5", TimeSpan.FromHours(-5), "m5", "m5"));

        Assert.AreEqual("9:26:42 AM", vm.TimeText);
    }

    [TestMethod]
    public void ApplyAppearance_sets_a_solid_brush_from_derived_color()
    {
        var vm = new DateTimeWidgetViewModel();

        vm.ApplyAppearance("#0F121D", 100);

        var brush = Assert.IsInstanceOfType<Avalonia.Media.SolidColorBrush>(vm.CardBackground);
        Assert.AreEqual(Avalonia.Media.Color.Parse("#FF0F121D"), brush.Color);
    }
}
