using System;
using System.Globalization;

namespace DesktopMetrics.Lib;

// Derives the card's two gradient stops from a single opaque base color and an
// opacity percentage. Pure and Avalonia-free; a view model turns the hex strings
// into a brush, mirroring the RowBuilder -> converter split.
public static class AppearanceGradient
{
    private const string DefaultBase = "#0F121D";
    private const double TopFactor = 1.33;
    private const double BottomFactor = 0.72;

    public static (string Top, string Bottom) Derive(string baseHex, int opacityPercent)
    {
        (byte red, byte green, byte blue) = ParseRgb(baseHex) ?? ParseRgb(DefaultBase)!.Value;

        int clampedOpacity = Math.Clamp(opacityPercent, 0, 100);
        byte alpha = (byte)Round(clampedOpacity / 100d * 255d);

        string top = Compose(alpha, red, green, blue, TopFactor);
        string bottom = Compose(alpha, red, green, blue, BottomFactor);
        return (top, bottom);
    }

    private static string Compose(byte alpha, byte red, byte green, byte blue, double factor)
    {
        byte scaledRed = Scale(red, factor);
        byte scaledGreen = Scale(green, factor);
        byte scaledBlue = Scale(blue, factor);
        return $"#{alpha:X2}{scaledRed:X2}{scaledGreen:X2}{scaledBlue:X2}";
    }

    private static byte Scale(byte channel, double factor)
        => (byte)Math.Clamp(Round(channel * factor), 0, 255);

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
