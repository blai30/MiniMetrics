using System;
using System.Collections.Generic;

namespace MiniMetrics.Lib;

// Pure, Avalonia-free snap math. Given a widget rectangle, a screen working area, and any peer
// widget rectangles (all in physical pixels), pulls the widget flush to the nearest edge it is
// within the threshold of. X and Y are evaluated independently, so dragging into a corner snaps
// both axes. Peers add flush-adjacency and edge-alignment targets alongside the screen edges.
public static class EdgeSnap
{
    public readonly record struct Rect(int X, int Y, int Width, int Height);

    public static (int X, int Y) Snap(Rect widget, Rect area, IReadOnlyList<Rect> peers, int threshold)
    {
        int x = SnapAxis(widget.X, widget.Width, area.X, area.Width, peers, xAxis: true, threshold);
        int y = SnapAxis(widget.Y, widget.Height, area.Y, area.Height, peers, xAxis: false, threshold);
        return (x, y);
    }

    // Snaps one axis to the nearest candidate within threshold. Candidates are gathered from the
    // screen edges first (so the leading screen edge wins ties) then each peer's adjacency and
    // alignment targets. Strict "<" keeps the earlier candidate when distances tie.
    private static int SnapAxis(int position, int size, int areaStart, int areaSize,
        IReadOnlyList<Rect> peers, bool xAxis, int threshold)
    {
        int areaEnd = areaStart + areaSize;
        int best = position;
        int bestDistance = threshold + 1;

        void Consider(int target)
        {
            int distance = Math.Abs(target - position);
            if (distance <= threshold && distance < bestDistance)
            {
                bestDistance = distance;
                best = target;
            }
        }

        // Screen edges: leading then trailing.
        Consider(areaStart);
        Consider(areaEnd - size);

        foreach (Rect peer in peers)
        {
            int peerStart = xAxis ? peer.X : peer.Y;
            int peerSize = xAxis ? peer.Width : peer.Height;
            int peerEnd = peerStart + peerSize;
            Consider(peerEnd);            // widget sits flush after the peer
            Consider(peerStart - size);   // widget sits flush before the peer
            Consider(peerStart);          // widget leading edge aligns with peer leading edge
            Consider(peerEnd - size);     // widget trailing edge aligns with peer trailing edge
        }

        return best;
    }
}
