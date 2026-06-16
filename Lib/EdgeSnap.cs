using System;

namespace MiniMetrics.Lib;

// Pure, Avalonia-free snap math. Given a widget rectangle and a screen working
// area (both in physical pixels), pulls the widget flush to any edge it is within
// the threshold of. X and Y are evaluated independently, so dragging into a corner
// snaps both axes and lands flush in the corner with no dedicated corner logic.
public static class EdgeSnap
{
    public readonly record struct Rect(int X, int Y, int Width, int Height);

    public static (int X, int Y) Snap(Rect widget, Rect area, int threshold)
    {
        int x = SnapAxis(widget.X, widget.Width, area.X, area.Width, threshold);
        int y = SnapAxis(widget.Y, widget.Height, area.Y, area.Height, threshold);
        return (x, y);
    }

    // Snaps one axis: the leading (left/top) edge wins if both ends are in range.
    private static int SnapAxis(int position, int size, int areaStart, int areaSize, int threshold)
    {
        int areaEnd = areaStart + areaSize;

        if (Math.Abs(position - areaStart) <= threshold)
        {
            return areaStart;
        }

        if (Math.Abs(position + size - areaEnd) <= threshold)
        {
            return areaEnd - size;
        }

        return position;
    }
}
