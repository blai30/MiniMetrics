using MiniMetrics.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class AppearanceColorTests
{
    [TestMethod]
    public void Derive_default_base_full_opacity_is_opaque_base()
    {
        var color = AppearanceColor.Derive("#0F121D", 100);
        Assert.AreEqual("#FF0F121D", color);
    }

    [TestMethod]
    public void Derive_applies_opacity_as_alpha()
    {
        var color = AppearanceColor.Derive("#0F121D", 50);

        // 50% of 255 = 127.5, rounded away from zero = 128 = 0x80.
        Assert.AreEqual("#800F121D", color);
    }

    [TestMethod]
    public void Derive_zero_opacity_is_fully_transparent()
    {
        var color = AppearanceColor.Derive("#0F121D", 0);
        Assert.AreEqual("#000F121D", color);
    }

    [TestMethod]
    public void Derive_clamps_opacity_above_100()
    {
        var color = AppearanceColor.Derive("#0F121D", 250);
        Assert.AreEqual("#FF0F121D", color);
    }

    [TestMethod]
    public void Derive_handles_white()
    {
        var color = AppearanceColor.Derive("#FFFFFF", 100);
        Assert.AreEqual("#FFFFFFFF", color);
    }

    [TestMethod]
    public void Derive_handles_black()
    {
        var color = AppearanceColor.Derive("#000000", 100);
        Assert.AreEqual("#FF000000", color);
    }

    [TestMethod]
    public void Derive_accepts_hex_without_leading_hash()
    {
        var color = AppearanceColor.Derive("0F121D", 100);
        Assert.AreEqual("#FF0F121D", color);
    }

    [TestMethod]
    public void Derive_falls_back_to_default_base_on_invalid_hex()
    {
        var color = AppearanceColor.Derive("not-a-color", 100);
        Assert.AreEqual("#FF0F121D", color);
    }
}
