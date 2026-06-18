using System;
using System.Globalization;
using MiniMetrics.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class ClockFormattingTests
{
    // A fixed UTC instant: 2026-06-16 14:26:42.
    private static readonly DateTimeOffset Instant =
        new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero);

    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo FrFr = CultureInfo.GetCultureInfo("fr-FR");

    private static TimeZoneInfo Offset(int hours) =>
        TimeZoneInfo.CreateCustomTimeZone($"Test{hours}", TimeSpan.FromHours(hours), $"Test{hours}", $"Test{hours}");

    [TestMethod]
    public void Render_blank_time_uses_default_long_time()
    {
        Assert.AreEqual("2:26:42 PM",
            ClockFormatting.Render(Instant, TimeZoneInfo.Utc, null, ClockFormatting.DefaultTimeFormat, EnUs));
    }

    [TestMethod]
    public void Render_blank_date_uses_default_long_date()
    {
        Assert.AreEqual("Tuesday, June 16, 2026",
            ClockFormatting.Render(Instant, TimeZoneInfo.Utc, "", ClockFormatting.DefaultDateFormat, EnUs));
    }

    [TestMethod]
    public void Render_hover_defaults_are_24_hour_time_and_utc_stamp()
    {
        Assert.AreEqual("14:26:42",
            ClockFormatting.Render(Instant, TimeZoneInfo.Utc, null, ClockFormatting.DefaultTimeFormatHover, EnUs));
        Assert.AreEqual("2026-06-16 14:26:42Z",
            ClockFormatting.Render(Instant, TimeZoneInfo.Utc, null, ClockFormatting.DefaultDateFormatHover, EnUs));
    }

    [TestMethod]
    public void Render_uses_a_valid_custom_format()
    {
        Assert.AreEqual("14:26",
            ClockFormatting.Render(Instant, TimeZoneInfo.Utc, "HH:mm", ClockFormatting.DefaultTimeFormat, EnUs));
    }

    [TestMethod]
    public void Render_falls_back_to_default_for_an_invalid_custom_format()
    {
        // A lone "h" is treated as a standard specifier and throws; the default must render instead.
        Assert.AreEqual("2:26:42 PM",
            ClockFormatting.Render(Instant, TimeZoneInfo.Utc, "h", ClockFormatting.DefaultTimeFormat, EnUs));
    }

    [TestMethod]
    public void Render_applies_the_supplied_culture()
    {
        Assert.AreEqual("mardi 16 juin 2026",
            ClockFormatting.Render(Instant, TimeZoneInfo.Utc, null, ClockFormatting.DefaultDateFormat, FrFr));
        // fr-FR long time is 24-hour.
        Assert.AreEqual("14:26:42",
            ClockFormatting.Render(Instant, TimeZoneInfo.Utc, null, ClockFormatting.DefaultTimeFormat, FrFr));
    }

    [TestMethod]
    public void Render_converts_zone_relative_formats_into_the_given_zone()
    {
        // 14:26:42 UTC in UTC-5 is 09:26:42.
        Assert.AreEqual("9:26:42 AM",
            ClockFormatting.Render(Instant, Offset(-5), null, ClockFormatting.DefaultTimeFormat, EnUs));
    }

    [TestMethod]
    public void Render_u_stays_utc_regardless_of_zone()
    {
        Assert.AreEqual("2026-06-16 14:26:42Z",
            ClockFormatting.Render(Instant, Offset(-8), null, ClockFormatting.DefaultDateFormatHover, EnUs));
    }

    [TestMethod]
    public void IsValidFormat_treats_blank_as_valid()
    {
        Assert.IsTrue(ClockFormatting.IsValidFormat(null, EnUs));
        Assert.IsTrue(ClockFormatting.IsValidFormat("", EnUs));
    }

    [TestMethod]
    public void IsValidFormat_accepts_a_well_formed_custom_format()
    {
        Assert.IsTrue(ClockFormatting.IsValidFormat("HH:mm:ss", EnUs));
    }

    [TestMethod]
    public void IsValidFormat_rejects_a_lone_standard_specifier_typo()
    {
        Assert.IsFalse(ClockFormatting.IsValidFormat("h", EnUs));
    }
}
