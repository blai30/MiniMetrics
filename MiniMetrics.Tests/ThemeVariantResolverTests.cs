using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.Tests;

[TestClass]
public class ThemeVariantResolverTests
{
    [TestMethod]
    public void Light_is_always_light()
    {
        Assert.IsFalse(ThemeVariantResolver.IsDark(AppTheme.Light, true));
        Assert.IsFalse(ThemeVariantResolver.IsDark(AppTheme.Light, false));
    }

    [TestMethod]
    public void Dark_is_always_dark()
    {
        Assert.IsTrue(ThemeVariantResolver.IsDark(AppTheme.Dark, true));
        Assert.IsTrue(ThemeVariantResolver.IsDark(AppTheme.Dark, false));
    }

    [TestMethod]
    public void System_defers_to_the_os()
    {
        Assert.IsTrue(ThemeVariantResolver.IsDark(AppTheme.System, true));
        Assert.IsFalse(ThemeVariantResolver.IsDark(AppTheme.System, false));
    }
}
