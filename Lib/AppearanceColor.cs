using System;
using System.Globalization;

namespace MiniMetrics.Lib;

// Derives the card's solid background color from a single opaque base color and an
// opacity percentage. Pure and Avalonia-free; a view model turns the hex string
// into a brush, mirroring the RowBuilder -> converter split.
public static class AppearanceColor
{
    private const string DefaultBase = "#0F121D";

    public static string Derive(string baseHex, int opacityPercent)
    {
        (byte red, byte green, byte blue) = ParseRgb(baseHex) ?? ParseRgb(DefaultBase)!.Value;

        int clampedOpacity = Math.Clamp(opacityPercent, 0, 100);
        byte alpha = (byte)Round(clampedOpacity / 100d * 255d);

        return $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";
    }

    private static int Round(double value)
        => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static (byte Red, byte Green, byte Blue)? ParseRgb(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        string trimmed = hex.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length != 6)
        {
            return null;
        }

        if (byte.TryParse(trimmed.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red)
            && byte.TryParse(trimmed.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green)
            && byte.TryParse(trimmed.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            return (red, green, blue);
        }

        return null;
    }
}
