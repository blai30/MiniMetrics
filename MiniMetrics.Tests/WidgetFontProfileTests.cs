using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.Tests;

[TestClass]
public class WidgetFontProfileTests
{
    [TestMethod]
    public void Null_family_resolves_to_bundled_inter()
    {
        var profile = WidgetFontProfile.Resolve(null, 100, WidgetFontWeight.Regular);
        Assert.AreEqual(WidgetFontProfile.BundledInter, profile.FontFamily);
    }

    [TestMethod]
    public void Empty_family_resolves_to_bundled_inter()
    {
        var profile = WidgetFontProfile.Resolve("", 100, WidgetFontWeight.Regular);
        Assert.AreEqual(WidgetFontProfile.BundledInter, profile.FontFamily);
    }

    [TestMethod]
    public void Inter_sentinel_resolves_to_bundled_source()
    {
        var profile = WidgetFontProfile.Resolve("Inter", 100, WidgetFontWeight.Regular);
        Assert.AreEqual(WidgetFontProfile.BundledInter, profile.FontFamily);
    }

    [TestMethod]
    public void System_family_name_passes_through()
    {
        var profile = WidgetFontProfile.Resolve("Cascadia Code", 100, WidgetFontWeight.Regular);
        Assert.AreEqual("Cascadia Code", profile.FontFamily);
    }

    [TestMethod]
    public void Scale_converts_percent_to_factor()
    {
        Assert.AreEqual(1.0, WidgetFontProfile.Resolve(null, 100, WidgetFontWeight.Regular).Scale, 1e-9);
        Assert.AreEqual(1.25, WidgetFontProfile.Resolve(null, 125, WidgetFontWeight.Regular).Scale, 1e-9);
    }

    [TestMethod]
    public void Scale_clamps_below_80_and_above_150()
    {
        Assert.AreEqual(0.80, WidgetFontProfile.Resolve(null, 50, WidgetFontWeight.Regular).Scale, 1e-9);
        Assert.AreEqual(0.80, WidgetFontProfile.Resolve(null, 79, WidgetFontWeight.Regular).Scale, 1e-9);
        Assert.AreEqual(1.50, WidgetFontProfile.Resolve(null, 151, WidgetFontWeight.Regular).Scale, 1e-9);
        Assert.AreEqual(1.50, WidgetFontProfile.Resolve(null, 999, WidgetFontWeight.Regular).Scale, 1e-9);
    }

    [TestMethod]
    public void Regular_preset_matches_todays_weights()
    {
        var profile = WidgetFontProfile.Resolve(null, 100, WidgetFontWeight.Regular);
        Assert.AreEqual(700, profile.StrongWeight);
        Assert.AreEqual(600, profile.UnitWeight);
        Assert.AreEqual(500, profile.ClockWeight);
    }

    [TestMethod]
    public void Light_preset_steps_every_role_down()
    {
        var profile = WidgetFontProfile.Resolve(null, 100, WidgetFontWeight.Light);
        Assert.AreEqual(600, profile.StrongWeight);
        Assert.AreEqual(500, profile.UnitWeight);
        Assert.AreEqual(400, profile.ClockWeight);
    }

    [TestMethod]
    public void Bold_preset_steps_every_role_up()
    {
        var profile = WidgetFontProfile.Resolve(null, 100, WidgetFontWeight.Bold);
        Assert.AreEqual(800, profile.StrongWeight);
        Assert.AreEqual(700, profile.UnitWeight);
        Assert.AreEqual(600, profile.ClockWeight);
    }
}
