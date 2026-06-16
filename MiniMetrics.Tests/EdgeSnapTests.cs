using MiniMetrics.Lib;
using Xunit;

namespace MiniMetrics.Tests;

public class EdgeSnapTests
{
    // A 1920x1080 work area at the origin, a 400x176 widget, 20px threshold.
    private static readonly EdgeSnap.Rect Area = new(0, 0, 1920, 1080);
    private const int Width = 400;
    private const int Height = 176;
    private const int Threshold = 20;

    private static (int X, int Y) Snap(int x, int y) =>
        EdgeSnap.Snap(new EdgeSnap.Rect(x, y, Width, Height), Area, Threshold);

    [Fact]
    public void Snaps_left_edge_when_within_threshold()
    {
        Assert.Equal((0, 500), Snap(12, 500));
    }

    [Fact]
    public void Does_not_snap_left_edge_beyond_threshold()
    {
        Assert.Equal((40, 500), Snap(40, 500));
    }

    [Fact]
    public void Snaps_left_edge_at_exact_threshold()
    {
        Assert.Equal((0, 500), Snap(20, 500));
    }

    [Fact]
    public void Snaps_right_edge_accounting_for_width()
    {
        // Right edge sits at 1920; widget left must be 1920 - 400 = 1520.
        Assert.Equal((1520, 500), Snap(1510, 500));
    }

    [Fact]
    public void Snaps_top_edge_when_within_threshold()
    {
        Assert.Equal((500, 0), Snap(500, 8));
    }

    [Fact]
    public void Snaps_bottom_edge_accounting_for_height()
    {
        // Bottom edge sits at 1080; widget top must be 1080 - 176 = 904.
        Assert.Equal((500, 904), Snap(500, 894));
    }

    [Fact]
    public void Snaps_both_axes_into_top_left_corner()
    {
        Assert.Equal((0, 0), Snap(10, 10));
    }

    [Fact]
    public void Snaps_both_axes_into_bottom_right_corner()
    {
        Assert.Equal((1520, 904), Snap(1515, 894));
    }

    [Fact]
    public void Leaves_position_untouched_when_far_from_all_edges()
    {
        Assert.Equal((800, 500), Snap(800, 500));
    }

    [Fact]
    public void Respects_non_zero_work_area_origin()
    {
        // Second monitor work area starting at x=1920.
        var area = new EdgeSnap.Rect(1920, 0, 1920, 1080);
        var result = EdgeSnap.Snap(new EdgeSnap.Rect(1930, 500, Width, Height), area, Threshold);
        Assert.Equal((1920, 500), result);
    }
}
