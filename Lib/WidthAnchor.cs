using System;
using MiniMetrics.Models;

namespace MiniMetrics.Lib;

// A widget that sizes to its content changes width whenever its text does, and a window's own anchor
// is always its top-left corner: it grows and shrinks from the right. That reads correctly only for
// left-aligned content. This recomputes the left edge so the aligned edge is the one that stays put.
public static class WidthAnchor
{
    // Returns the left edge, in physical pixels, that keeps the aligned edge of a widget fixed when
    // its logical width changes from oldWidth to newWidth at the given render scaling.
    public static int AnchoredLeft(int left, double oldWidth, double newWidth, ClockAlignment alignment, double renderScaling)
    {
        double delta = (newWidth - oldWidth) * renderScaling;
        double shift = alignment switch
        {
            ClockAlignment.Right => delta,
            ClockAlignment.Center => delta / 2,
            _ => 0
        };

        return left - (int)Math.Round(shift, MidpointRounding.AwayFromZero);
    }
}
