using System.Collections.Generic;
using System.Linq;
using MiniMetrics.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class MetricRegistryTests
{
    [TestMethod]
    public void Keys_are_unique()
    {
        var keys = MetricRegistry.All.Select(entry => entry.Key).ToList();

        Assert.AreEqual(keys.Count, keys.Distinct().Count());
    }

    [TestMethod]
    public void Cards_are_the_distinct_card_keys_in_declaration_order()
    {
        CollectionAssert.AreEqual(new[] { "cpu", "ram", "gpu", "vram" }, MetricRegistry.Cards.ToArray());
    }

    [TestMethod]
    public void ForCard_returns_only_that_cards_metrics()
    {
        var keys = MetricRegistry.ForCard("cpu").Select(entry => entry.Key);

        CollectionAssert.AreEqual(new[] { "cpu.usage", "cpu.temp", "cpu.power" }, keys.ToArray());
    }

    [TestMethod]
    public void AnyVisible_defaults_absent_keys_to_visible()
    {
        Assert.IsTrue(MetricRegistry.AnyVisible("cpu", new Dictionary<string, bool>()));
    }

    [TestMethod]
    public void AnyVisible_is_false_when_every_card_metric_is_explicitly_off()
    {
        var visibility = new Dictionary<string, bool>
        {
            ["cpu.usage"] = false,
            ["cpu.temp"] = false,
            ["cpu.power"] = false,
        };
        Assert.IsFalse(MetricRegistry.AnyVisible("cpu", visibility));
    }

    [TestMethod]
    public void AnyVisible_is_true_when_one_card_metric_is_on()
    {
        var visibility = new Dictionary<string, bool>
        {
            ["gpu.usage"] = false,
            ["gpu.temp"] = false,
            ["gpu.power"] = false,
            ["vram.usage"] = true,
        };
        Assert.IsTrue(MetricRegistry.AnyVisible("vram", visibility));
    }

    [TestMethod]
    public void Only_cpu_temp_and_power_require_elevation()
    {
        var elevated = MetricRegistry.All
            .Where(entry => entry.RequiresElevation)
            .Select(entry => entry.Key);

        CollectionAssert.AreEqual(new[] { "cpu.temp", "cpu.power" }, elevated.ToArray());
    }

    [TestMethod]
    public void RequiresElevation_defaults_absent_elevation_keys_to_off()
    {
        Assert.IsFalse(MetricRegistry.RequiresElevation(new Dictionary<string, bool>()));
    }

    [TestMethod]
    public void RequiresElevation_is_false_when_elevation_metrics_are_explicitly_off()
    {
        var visibility = new Dictionary<string, bool> { ["cpu.temp"] = false, ["cpu.power"] = false };
        Assert.IsFalse(MetricRegistry.RequiresElevation(visibility));
    }

    [TestMethod]
    [DataRow("cpu.temp")]
    [DataRow("cpu.power")]
    public void RequiresElevation_is_true_when_an_elevation_metric_is_on(string key)
    {
        var visibility = new Dictionary<string, bool> { [key] = true };
        Assert.IsTrue(MetricRegistry.RequiresElevation(visibility));
    }
}
