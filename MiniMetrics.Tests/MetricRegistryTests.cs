using System.Collections.Generic;
using System.Linq;
using MiniMetrics.Lib;
using Xunit;

namespace MiniMetrics.Tests;

public class MetricRegistryTests
{
    [Fact]
    public void Keys_are_unique()
    {
        var keys = MetricRegistry.All.Select(entry => entry.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Cards_are_the_distinct_card_keys_in_declaration_order()
    {
        Assert.Equal(new[] { "cpu", "ram", "gpu", "vram" }, MetricRegistry.Cards);
    }

    [Fact]
    public void ForCard_returns_only_that_cards_metrics()
    {
        var keys = MetricRegistry.ForCard("cpu").Select(entry => entry.Key);

        Assert.Equal(new[] { "cpu.usage", "cpu.temp", "cpu.power" }, keys);
    }

    [Fact]
    public void Only_cpu_temp_and_power_require_elevation()
    {
        var elevated = MetricRegistry.All
            .Where(entry => entry.RequiresElevation)
            .Select(entry => entry.Key);

        Assert.Equal(new[] { "cpu.temp", "cpu.power" }, elevated);
    }

    [Fact]
    public void RequiresElevation_defaults_absent_elevation_keys_to_off()
    {
        Assert.False(MetricRegistry.RequiresElevation(new Dictionary<string, bool>()));
    }

    [Fact]
    public void RequiresElevation_is_false_when_elevation_metrics_are_explicitly_off()
    {
        var visibility = new Dictionary<string, bool> { ["cpu.temp"] = false, ["cpu.power"] = false };
        Assert.False(MetricRegistry.RequiresElevation(visibility));
    }

    [Theory]
    [InlineData("cpu.temp")]
    [InlineData("cpu.power")]
    public void RequiresElevation_is_true_when_an_elevation_metric_is_on(string key)
    {
        var visibility = new Dictionary<string, bool> { [key] = true };
        Assert.True(MetricRegistry.RequiresElevation(visibility));
    }
}
