using MiniMetrics.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class MetricFormattingTests
{
    [TestMethod]
    [DataRow(34.4, "34")]
    [DataRow(77.6, "78")]
    public void FormatPercent_rounds_to_a_bare_whole_number(double value, string expected)
    {
        Assert.AreEqual(expected, MetricFormatting.FormatPercent(value));
    }

    [TestMethod]
    public void FormatGiB_uses_one_decimal_by_default()
    {
        // 12,026,124,800 bytes is 11.2 GiB.
        Assert.AreEqual("11.2", MetricFormatting.FormatGiB(12_026_124_800UL));
    }

    [TestMethod]
    public void FormatGiB_supports_zero_decimals_for_totals()
    {
        // 34,359,738,368 bytes is exactly 32 GiB.
        Assert.AreEqual("32", MetricFormatting.FormatGiB(34_359_738_368UL, 0));
    }

    [TestMethod]
    public void FormatTempValue_rounds_to_a_bare_whole_number()
    {
        Assert.AreEqual("62", MetricFormatting.FormatTempValue(62.0));
    }

    [TestMethod]
    public void FormatPower_appends_watts()
    {
        Assert.AreEqual("185 W", MetricFormatting.FormatPower(185.0));
    }
}
