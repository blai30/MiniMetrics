using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.Tests;

[TestClass]
public class WidthAnchorTests
{
    [TestMethod]
    public void Left_alignment_keeps_the_left_edge_where_it_is()
    {
        Assert.AreEqual(1000, WidthAnchor.AnchoredLeft(1000, 500, 400, ClockAlignment.Left, 1.0));
        Assert.AreEqual(1000, WidthAnchor.AnchoredLeft(1000, 500, 600, ClockAlignment.Left, 1.0));
    }

    [TestMethod]
    public void Right_alignment_absorbs_the_whole_width_change()
    {
        // Shrinking by 100 pulls the left edge right by 100, so the right edge stays at 1500.
        Assert.AreEqual(1100, WidthAnchor.AnchoredLeft(1000, 500, 400, ClockAlignment.Right, 1.0));
        Assert.AreEqual(900, WidthAnchor.AnchoredLeft(1000, 500, 600, ClockAlignment.Right, 1.0));
    }

    [TestMethod]
    public void Center_alignment_absorbs_half_the_width_change()
    {
        Assert.AreEqual(1050, WidthAnchor.AnchoredLeft(1000, 500, 400, ClockAlignment.Center, 1.0));
        Assert.AreEqual(950, WidthAnchor.AnchoredLeft(1000, 500, 600, ClockAlignment.Center, 1.0));
    }

    [TestMethod]
    public void Width_change_is_scaled_to_physical_pixels()
    {
        // 100 logical pixels narrower at 150% scaling is 150 physical pixels.
        Assert.AreEqual(1150, WidthAnchor.AnchoredLeft(1000, 500, 400, ClockAlignment.Right, 1.5));
    }

    [TestMethod]
    public void A_fractional_shift_is_rounded_to_a_whole_pixel()
    {
        Assert.AreEqual(1003, WidthAnchor.AnchoredLeft(1000, 500, 495, ClockAlignment.Center, 1.0));
    }

    [TestMethod]
    public void An_unchanged_width_leaves_the_position_alone()
    {
        Assert.AreEqual(1000, WidthAnchor.AnchoredLeft(1000, 500, 500, ClockAlignment.Right, 1.0));
    }
}
