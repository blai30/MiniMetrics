using MiniMetrics.Models;

namespace MiniMetrics.Lib;

// Maps the user's theme choice and the OS theme to a single is-dark flag. Pure and Avalonia-free so
// it is verified without an Avalonia app running; App.axaml.cs supplies systemIsDark from Avalonia's
// resolved ActualThemeVariant.
public static class ThemeVariantResolver
{
    public static bool IsDark(AppTheme theme, bool systemIsDark) => theme switch
    {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => systemIsDark,
    };
}
