using MiniMetrics.Lib;

namespace MiniMetrics.Tests;

[TestClass]
public class EdgeSnapTests
{
    // A 1920x1080 work area at the origin, a 400x176 widget, 20px threshold.
    private static readonly EdgeSnap.Rect Area = new(0, 0, 1920, 1080);
    private const int Width = 400;
    private const int Height = 176;
    private const int Threshold = 20;

    private static readonly EdgeSnap.Rect[] NoPeers = [];

    private static (int X, int Y) Snap(int x, int y) =>
        EdgeSnap.Snap(new(x, y, Width, Height), Area, NoPeers, Threshold);

    [TestMethod]
    public void Snaps_left_edge_when_within_threshold()
    {
        Assert.AreEqual((0, 500), Snap(12, 500));
    }

    [TestMethod]
    public void Does_not_snap_left_edge_beyond_threshold()
    {
        Assert.AreEqual((40, 500), Snap(40, 500));
    }

    [TestMethod]
    public void Snaps_left_edge_at_exact_threshold()
    {
        Assert.AreEqual((0, 500), Snap(20, 500));
    }

    [TestMethod]
    public void Snaps_right_edge_accounting_for_width()
    {
        // Right edge sits at 1920; widget left must be 1920 - 400 = 1520.
        Assert.AreEqual((1520, 500), Snap(1510, 500));
    }

    [TestMethod]
    public void Snaps_top_edge_when_within_threshold()
    {
        Assert.AreEqual((500, 0), Snap(500, 8));
    }

    [TestMethod]
    public void Snaps_bottom_edge_accounting_for_height()
    {
        // Bottom edge sits at 1080; widget top must be 1080 - 176 = 904.
        Assert.AreEqual((500, 904), Snap(500, 894));
    }

    [TestMethod]
    public void Snaps_both_axes_into_top_left_corner()
    {
        Assert.AreEqual((0, 0), Snap(10, 10));
    }

    [TestMethod]
    public void Snaps_both_axes_into_bottom_right_corner()
    {
        Assert.AreEqual((1520, 904), Snap(1515, 894));
    }

    [TestMethod]
    public void Leaves_position_untouched_when_far_from_all_edges()
    {
        Assert.AreEqual((800, 500), Snap(800, 500));
    }

    [TestMethod]
    public void Respects_non_zero_work_area_origin()
    {
        // Second monitor work area starting at x=1920.
        var area = new EdgeSnap.Rect(1920, 0, 1920, 1080);
        var result = EdgeSnap.Snap(new(1930, 500, Width, Height), area, NoPeers, Threshold);
        Assert.AreEqual((1920, 500), result);
    }

    [TestMethod]
    public void Snaps_flush_below_a_peer_and_aligns_left_edges()
    {
        // Peer (metrics) at (100,100) sized 400x176: bottom edge at 276, left at 100.
        var peer = new EdgeSnap.Rect(100, 100, 400, 176);
        // Dragged widget 640x176 left-aligned, 9px below the peer's bottom.
        var result = EdgeSnap.Snap(new(100, 285, 640, 176), Area, [peer], Threshold);
        Assert.AreEqual((100, 276), result);
    }

    [TestMethod]
    public void Snaps_flush_above_a_peer()
    {
        // Peer top edge at 500; a 176-tall widget whose bottom is near 500 snaps to top = 324.
        var peer = new EdgeSnap.Rect(100, 500, 400, 176);
        var result = EdgeSnap.Snap(new(100, 315, 640, 176), Area, [peer], Threshold);
        Assert.AreEqual((100, 324), result);
    }

    [TestMethod]
    public void Aligns_left_edge_to_a_peer_without_snapping_the_other_axis()
    {
        var peer = new EdgeSnap.Rect(300, 200, 400, 176);
        // x is 10px from the peer's left edge; y is far from every edge and the peer.
        var result = EdgeSnap.Snap(new(290, 600, 640, 176), Area, [peer], Threshold);
        Assert.AreEqual((300, 600), result);
    }

    [TestMethod]
    public void Does_not_snap_to_a_peer_beyond_threshold()
    {
        var peer = new EdgeSnap.Rect(100, 100, 400, 176);
        // 44px below the peer (> 20) and far from peer x-edges and screen edges.
        var result = EdgeSnap.Snap(new(600, 320, 640, 176), Area, [peer], Threshold);
        Assert.AreEqual((600, 320), result);
    }
}
