using System;
using MiniMetrics.Lib;
using Xunit;

namespace MiniMetrics.Tests;

public class ClockFormattingTests
{
    // A fixed UTC instant: 2026-06-16 14:26:42.
    private static readonly DateTimeOffset Instant =
        new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero);

    private static TimeZoneInfo Offset(int hours) =>
        TimeZoneInfo.CreateCustomTimeZone($"Test{hours}", TimeSpan.FromHours(hours), $"Test{hours}", $"Test{hours}");

    [Fact]
    public void FormatTime_12_hour_has_no_leading_zero_and_meridiem()
    {
        Assert.Equal("2:26:42 PM", ClockFormatting.FormatTime(Instant, TimeZoneInfo.Utc, use24Hour: false));
    }

    [Fact]
    public void FormatTime_24_hour_is_zero_padded_without_meridiem()
    {
        Assert.Equal("14:26:42", ClockFormatting.FormatTime(Instant, TimeZoneInfo.Utc, use24Hour: true));
    }

    [Fact]
    public void FormatTime_converts_into_the_given_zone()
    {
        // UTC-5 turns 14:26:42 into 09:26:42.
        Assert.Equal("9:26:42 AM", ClockFormatting.FormatTime(Instant, Offset(-5), use24Hour: false));
    }

    [Fact]
    public void FormatTime_renders_midnight_as_12_am()
    {
        var midnight = new DateTimeOffset(2026, 6, 16, 0, 0, 5, TimeSpan.Zero);
        Assert.Equal("12:00:05 AM", ClockFormatting.FormatTime(midnight, TimeZoneInfo.Utc, use24Hour: false));
    }

    [Fact]
    public void FormatTime_renders_noon_as_12_pm()
    {
        var noon = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal("12:00:00 PM", ClockFormatting.FormatTime(noon, TimeZoneInfo.Utc, use24Hour: false));
    }

    [Fact]
    public void FormatDate_uses_full_weekday_and_month_in_english()
    {
        Assert.Equal("Tuesday, June 16, 2026", ClockFormatting.FormatDate(Instant, TimeZoneInfo.Utc));
    }

    [Fact]
    public void FormatDate_converts_into_the_given_zone()
    {
        // 2026-06-16 01:00 UTC in UTC-5 is still 2026-06-15 20:00.
        var lateNight = new DateTimeOffset(2026, 6, 16, 1, 0, 0, TimeSpan.Zero);
        Assert.Equal("Monday, June 15, 2026", ClockFormatting.FormatDate(lateNight, Offset(-5)));
    }
}
