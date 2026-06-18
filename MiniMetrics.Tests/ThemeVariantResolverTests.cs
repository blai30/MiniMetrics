using MiniMetrics.Lib;
using MiniMetrics.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class ThemeVariantResolverTests
{
    [TestMethod]
    public void Light_is_always_light()
    {
        Assert.IsFalse(ThemeVariantResolver.IsDark(AppTheme.Light, systemIsDark: true));
        Assert.IsFalse(ThemeVariantResolver.IsDark(AppTheme.Light, systemIsDark: false));
    }

    [TestMethod]
    public void Dark_is_always_dark()
    {
        Assert.IsTrue(ThemeVariantResolver.IsDark(AppTheme.Dark, systemIsDark: true));
        Assert.IsTrue(ThemeVariantResolver.IsDark(AppTheme.Dark, systemIsDark: false));
    }

    [TestMethod]
    public void System_defers_to_the_os()
    {
        Assert.IsTrue(ThemeVariantResolver.IsDark(AppTheme.System, systemIsDark: true));
        Assert.IsFalse(ThemeVariantResolver.IsDark(AppTheme.System, systemIsDark: false));
    }
}
