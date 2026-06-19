using MiniMetrics.Lib;

namespace MiniMetrics.Tests;

[TestClass]
public class ThemePaletteTests
{
    [TestMethod]
    public void Dark_bar_gradient_matches_the_current_values()
    {
        Assert.AreEqual(("#0EA5E9", "#67E8F9"), ThemePalette.BarGradient(RowColor.Cyan, true));
        Assert.AreEqual(("#8B5CF6", "#C4B5FD"), ThemePalette.BarGradient(RowColor.Violet, true));
    }

    [TestMethod]
    public void Light_bar_gradient_darkens_the_pale_end()
    {
        Assert.AreEqual(("#0284C7", "#0EA5E9"), ThemePalette.BarGradient(RowColor.Cyan, false));
        Assert.AreEqual(("#D97706", "#F59E0B"), ThemePalette.BarGradient(RowColor.Amber, false));
    }

    [TestMethod]
    public void Dark_temp_color_matches_the_current_values()
    {
        Assert.AreEqual("#7DD3FC", ThemePalette.TempColor(TempLevel.Frigid, true));
        Assert.AreEqual("#F87171", ThemePalette.TempColor(TempLevel.Critical, true));
    }

    [TestMethod]
    public void Light_temp_color_is_legible_on_a_light_card()
    {
        Assert.AreEqual("#0284C7", ThemePalette.TempColor(TempLevel.Frigid, false));
        Assert.AreEqual("#DC2626", ThemePalette.TempColor(TempLevel.Critical, false));
    }
}
