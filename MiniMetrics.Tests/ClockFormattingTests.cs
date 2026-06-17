using System;
using MiniMetrics.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class ClockFormattingTests
{
    // A fixed UTC instant: 2026-06-16 14:26:42.
    private static readonly DateTimeOffset Instant =
        new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero);

    private static TimeZoneInfo Offset(int hours) =>
        TimeZoneInfo.CreateCustomTimeZone($"Test{hours}", TimeSpan.FromHours(hours), $"Test{hours}", $"Test{hours}");

    [TestMethod]
    public void FormatTime_12_hour_has_no_leading_zero_and_meridiem()
    {
        Assert.AreEqual("2:26:42 PM", ClockFormatting.FormatTime(Instant, TimeZoneInfo.Utc, use24Hour: false));
    }

    [TestMethod]
    public void FormatTime_24_hour_is_zero_padded_without_meridiem()
    {
        Assert.AreEqual("14:26:42", ClockFormatting.FormatTime(Instant, TimeZoneInfo.Utc, use24Hour: true));
    }

    [TestMethod]
    public void FormatTime_converts_into_the_given_zone()
    {
        // UTC-5 turns 14:26:42 into 09:26:42.
        Assert.AreEqual("9:26:42 AM", ClockFormatting.FormatTime(Instant, Offset(-5), use24Hour: false));
    }

    [TestMethod]
    public void FormatTime_renders_midnight_as_12_am()
    {
        var midnight = new DateTimeOffset(2026, 6, 16, 0, 0, 5, TimeSpan.Zero);
        Assert.AreEqual("12:00:05 AM", ClockFormatting.FormatTime(midnight, TimeZoneInfo.Utc, use24Hour: false));
    }

    [TestMethod]
    public void FormatTime_renders_noon_as_12_pm()
    {
        var noon = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);
        Assert.AreEqual("12:00:00 PM", ClockFormatting.FormatTime(noon, TimeZoneInfo.Utc, use24Hour: false));
    }

    [TestMethod]
    public void FormatDate_uses_full_weekday_and_month_in_english()
    {
        Assert.AreEqual("Tuesday, June 16, 2026", ClockFormatting.FormatDate(Instant, TimeZoneInfo.Utc));
    }

    [TestMethod]
    public void FormatDate_converts_into_the_given_zone()
    {
        // 2026-06-16 01:00 UTC in UTC-5 is still 2026-06-15 20:00.
        var lateNight = new DateTimeOffset(2026, 6, 16, 1, 0, 0, TimeSpan.Zero);
        Assert.AreEqual("Monday, June 15, 2026", ClockFormatting.FormatDate(lateNight, Offset(-5)));
    }

    [TestMethod]
    public void FormatDate_appends_negative_zone_offset_when_requested()
    {
        Assert.AreEqual("Tuesday, June 16, 2026  UTC-08:00", ClockFormatting.FormatDate(Instant, Offset(-8), showZone: true));
    }

    [TestMethod]
    public void FormatDate_appends_positive_and_utc_zone_offsets_when_requested()
    {
        Assert.AreEqual("Tuesday, June 16, 2026  UTC+05:00", ClockFormatting.FormatDate(Instant, Offset(5), showZone: true));
        Assert.AreEqual("Tuesday, June 16, 2026  UTC+00:00", ClockFormatting.FormatDate(Instant, TimeZoneInfo.Utc, showZone: true));
    }
}
