using MiniMetrics.Lib;
using Xunit;

namespace MiniMetrics.Tests;

public class MetricFormattingTests
{
    [Theory]
    [InlineData(34.4, "34")]
    [InlineData(77.6, "78")]
    public void FormatPercent_rounds_to_a_bare_whole_number(double value, string expected)
    {
        Assert.Equal(expected, MetricFormatting.FormatPercent(value));
    }

    [Fact]
    public void FormatGiB_uses_one_decimal_by_default()
    {
        // 12,026,124,800 bytes is 11.2 GiB.
        Assert.Equal("11.2", MetricFormatting.FormatGiB(12_026_124_800UL));
    }

    [Fact]
    public void FormatGiB_supports_zero_decimals_for_totals()
    {
        // 34,359,738,368 bytes is exactly 32 GiB.
        Assert.Equal("32", MetricFormatting.FormatGiB(34_359_738_368UL, 0));
    }

    [Fact]
    public void FormatTempValue_rounds_to_a_bare_whole_number()
    {
        Assert.Equal("62", MetricFormatting.FormatTempValue(62.0));
    }

    [Fact]
    public void FormatPower_appends_watts()
    {
        Assert.Equal("185W", MetricFormatting.FormatPower(185.0));
    }
}
