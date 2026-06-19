using System;
using MiniMetrics.Models;

namespace MiniMetrics.Lib;

// Turns the three saved font settings into the values the widgets bind to. Pure and Avalonia-free:
// weights are returned as numeric OpenType values (the integers behind Avalonia's FontWeight), which
// the view models cast to FontWeight at the boundary.
public readonly record struct WidgetFontProfile(
    string FontFamily,
    double Scale,
    int StrongWeight,
    int UnitWeight,
    int ClockWeight)
{
    // The bundled Inter font, referenced by its embedded resource uri. A bare "Inter" name only
    // resolves if Inter is also installed system-wide, so the default and the "Inter" choice both map
    // to this source.
    public const string BundledInter = "avares://Avalonia.Fonts.Inter/Assets#Inter";

    // The friendly name shown for the bundled font and used as the sentinel for it.
    public const string DefaultFamilyName = "Inter";

    private const int MinScalePercent = 80;
    private const int MaxScalePercent = 150;

    public static WidgetFontProfile Resolve(string? family, int scalePercent, WidgetFontWeight weight)
    {
        string resolvedFamily = string.IsNullOrEmpty(family) || family == DefaultFamilyName
            ? BundledInter
            : family;

        double scale = Math.Clamp(scalePercent, MinScalePercent, MaxScalePercent) / 100.0;

        // One coordinated step per preset keeps the strong-over-unit hierarchy intact at every preset.
        (int strong, int unit, int clock) = weight switch
        {
            WidgetFontWeight.Light => (600, 500, 400),
            WidgetFontWeight.Bold => (800, 700, 600),
            _ => (700, 600, 500)
        };

        return new(resolvedFamily, scale, strong, unit, clock);
    }
}
