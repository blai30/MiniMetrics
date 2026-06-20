using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace MiniMetrics.Views;

// Single source of truth for every lucide icon used anywhere in the app (tray, settings, dialogs).
// Each icon is stored as the array of its lucide sub-paths: each lucide <path> verbatim, and any
// rect/circle/line element rewritten as one path command string. Sub-paths are parsed and stroked
// independently (see Draw), so a leading lowercase 'm' on a sub-path is an absolute moveto.
internal static class LucideIcons
{
    // lucide authors on a 24x24 grid with a 2px stroke.
    private const double ViewBox = 24.0;

    private static readonly Dictionary<string, string[]> Paths = new()
    {
        // Tray
        ["cpu"] =
        [
            "M12 20v2", "M12 2v2", "M17 20v2", "M17 2v2", "M2 12h2", "M2 17h2", "M2 7h2",
            "M20 12h2", "M20 17h2", "M20 7h2", "M7 20v2", "M7 2v2",
            "M6 4h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-12a2 2 0 0 1-2-2v-12a2 2 0 0 1 2-2z",
            "M9 8h6a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1h-6a1 1 0 0 1-1-1v-6a1 1 0 0 1 1-1z"
        ],
        ["monitor"] =
        [
            "M4 3h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2h-16a2 2 0 0 1-2-2v-10a2 2 0 0 1 2-2z",
            "M8 21h8", "M12 17v4"
        ],
        ["clock"] = ["M2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0z", "M12 6v6l4 2"],
        ["lock"] =
        [
            "M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-14a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z",
            "M7 11V7a5 5 0 0 1 10 0v4"
        ],
        ["arrow-up-to-line"] = ["M5 3h14", "m18 13-6-6-6 6", "M12 7v14"],
        ["frame"] = ["M22 6H2", "M22 18H2", "M6 2V22", "M18 2V22"],
        ["power"] = ["M12 2v10", "M18.4 6.6a9 9 0 1 1-12.77.04"],
        ["settings"] =
        [
            "M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915",
            "M9 12a3 3 0 1 0 6 0a3 3 0 1 0-6 0z"
        ],
        ["refresh-cw"] =
        [
            "M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8", "M21 3v5h-5",
            "M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16", "M8 16H3v5"
        ],
        ["trash-2"] =
        [
            "M10 11v6", "M14 11v6", "M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6", "M3 6h18",
            "M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"
        ],
        ["log-out"] = ["m16 17 5-5-5-5", "M21 12H9", "M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"],
        ["download"] = ["M12 15V3", "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4", "m7 10 5 5 5-5"],

        // Nav rail
        ["palette"] =
        [
            "M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z",
            "M13 6.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0-1 0z",
            "M17 10.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0-1 0z",
            "M6 12.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0-1 0z",
            "M8 7.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0-1 0z"
        ],
        ["gauge"] = ["m12 14 4-4", "M3.34 19a10 10 0 1 1 17.32 0"],

        // Settings-card headers
        ["sun-moon"] =
        [
            "M12 2v2",
            "M14.837 16.385a6 6 0 1 1-7.223-7.222c.624-.147.97.66.715 1.248a4 4 0 0 0 5.26 5.259c.589-.255 1.396.09 1.248.715",
            "M16 12a4 4 0 0 0-4-4", "m19 5-1.256 1.256", "M20 12h2"
        ],
        ["blend"] = ["M2 9a7 7 0 1 0 14 0a7 7 0 1 0-14 0z", "M8 15a7 7 0 1 0 14 0a7 7 0 1 0-14 0z"],
        ["scaling"] =
        [
            "M12 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7",
            "M14 15H9v-5", "M16 3h5v5", "M21 3 9 15"
        ],
        ["type"] = ["M12 4v16", "M4 7V5a1 1 0 0 1 1-1h14a1 1 0 0 1 1 1v2", "M9 20h6"],
        ["rows-2"] =
        [
            "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-14a2 2 0 0 1-2-2v-14a2 2 0 0 1 2-2z",
            "M3 12h18"
        ],
        ["globe"] =
        [
            "M2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0z",
            "M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20", "M2 12h20"
        ],
        ["languages"] = ["m5 8 6 6", "m4 14 6-6 2-3", "M2 5h12", "M7 2h1", "m22 22-5-10-5 10", "M14 18h6"],
        ["text-align-start"] = ["M21 5H3", "M15 12H3", "M17 19H3"],
        ["calendar-clock"] =
        [
            "M16 14v2.2l1.6 1", "M16 2v4",
            "M21 7.5V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h3.5",
            "M3 10h5", "M8 2v4", "M10 16a6 6 0 1 0 12 0a6 6 0 1 0-12 0z"
        ],
        ["square-mouse-pointer"] =
        [
            "M12.034 12.681a.498.498 0 0 1 .647-.647l9 3.5a.5.5 0 0 1-.033.943l-3.444 1.068a1 1 0 0 0-.66.66l-1.067 3.443a.5.5 0 0 1-.943.033z",
            "M21 11V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h6"
        ],
        ["timer"] = ["M10 2L14 2", "M12 14L15 11", "M4 14a8 8 0 1 0 16 0a8 8 0 1 0-16 0z"],

        // Standalone
        ["rotate-ccw"] = ["M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8", "M3 3v5h5"],
        ["chevron-down"] = ["m6 9 6 6 6-6"],

        // Dialogs
        ["shield"] =
        [
            "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"
        ]
    };

    // Built and read on the UI thread only (tray construction, theme changes, LucideIcon.Render). Not
    // thread-safe; do not call Get from a background thread.
    private static readonly Dictionary<string, Geometry> Cache = [];

    public static Geometry? Get(string name)
    {
        if (Cache.TryGetValue(name, out var cached)) return cached;
        if (!Paths.TryGetValue(name, out string[]? parts)) return null;

        var group = new GeometryGroup();
        foreach (string part in parts) group.Children.Add(Geometry.Parse(part));
        Cache[name] = group;
        return group;
    }

    // Fits the 24-unit grid into a boxSize square with a half-stroke inset so round caps are not clipped,
    // then strokes each sub-path independently with the supplied pen.
    public static void Draw(DrawingContext context, Geometry geometry, IPen pen, double boxSize)
    {
        double strokeWidth = pen.Thickness;
        double inset = strokeWidth / 2.0;
        double scale = (boxSize - strokeWidth) / ViewBox;

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(inset, inset)))
        {
            if (geometry is GeometryGroup group)
                foreach (var child in group.Children)
                    context.DrawGeometry(null, pen, child);
            else
                context.DrawGeometry(null, pen, geometry);
        }
    }
}
