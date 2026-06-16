using MiniMetrics.Lib;
using Xunit;

namespace MiniMetrics.Tests;

public class AppearanceGradientTests
{
    [Fact]
    public void Derive_default_base_full_opacity_matches_premium_look()
    {
        var (top, bottom) = AppearanceGradient.Derive("#0F121D", 100);

        Assert.Equal("#FF141827", top);
        Assert.Equal("#FF0B0D15", bottom);
    }

    [Fact]
    public void Derive_applies_opacity_as_alpha()
    {
        var (top, bottom) = AppearanceGradient.Derive("#0F121D", 50);

        // 50% of 255 = 127.5, rounded away from zero = 128 = 0x80.
        Assert.Equal("#80141827", top);
        Assert.Equal("#800B0D15", bottom);
    }

    [Fact]
    public void Derive_zero_opacity_is_fully_transparent()
    {
        var (top, _) = AppearanceGradient.Derive("#0F121D", 0);
        Assert.Equal("#00141827", top);
    }

    [Fact]
    public void Derive_clamps_opacity_above_100()
    {
        var (top, _) = AppearanceGradient.Derive("#0F121D", 250);
        Assert.Equal("#FF141827", top);
    }

    [Fact]
    public void Derive_clamps_bright_channels_at_255()
    {
        var (top, bottom) = AppearanceGradient.Derive("#FFFFFF", 100);

        // White brightened stays white; darkened is 255 * 0.72 = 183.6 -> 184 = 0xB8.
        Assert.Equal("#FFFFFFFF", top);
        Assert.Equal("#FFB8B8B8", bottom);
    }

    [Fact]
    public void Derive_handles_black()
    {
        var (top, bottom) = AppearanceGradient.Derive("#000000", 100);
        Assert.Equal("#FF000000", top);
        Assert.Equal("#FF000000", bottom);
    }

    [Fact]
    public void Derive_accepts_hex_without_leading_hash()
    {
        var (top, _) = AppearanceGradient.Derive("0F121D", 100);
        Assert.Equal("#FF141827", top);
    }

    [Fact]
    public void Derive_falls_back_to_default_base_on_invalid_hex()
    {
        var (top, _) = AppearanceGradient.Derive("not-a-color", 100);
        Assert.Equal("#FF141827", top);
    }
}
