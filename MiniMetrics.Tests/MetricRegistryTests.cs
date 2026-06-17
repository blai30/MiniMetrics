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
}
