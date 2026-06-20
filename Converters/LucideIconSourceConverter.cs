using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using MiniMetrics.Views;

namespace MiniMetrics.Converters;

// Produces a themed lucide FAImageIconSource for the FluentAvalonia settings-card icon slots, which only
// accept an FAIconSource and cannot host a stroked vector. Bind the card's IconSource to its own
// ActualThemeVariant with the lucide name as ConverterParameter; the icon re-rasterizes when the theme
// changes. Results are cached per (name, variant).
public sealed class LucideIconSourceConverter : IValueConverter
{
    // Populated and read on the UI thread only (XAML binding evaluation). Not thread-safe.
    private static readonly Dictionary<(string Name, ThemeVariant Variant), FAImageIconSource> Cache = [];

    // Thinner than lucide's default 2.0 so the rasterized card icons read lighter.
    private const double StrokeWidth = 1.5;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string name) return null;
        var variant = value as ThemeVariant ?? ThemeVariant.Default;

        if (Cache.TryGetValue((name, variant), out var cached)) return cached;

        var iconSource = new FAImageIconSource
            { Source = MenuIconRenderer.Render(name, ResolveBrush(variant), StrokeWidth) };
        Cache[(name, variant)] = iconSource;
        return iconSource;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    // Match the vector icons' color: use the theme's primary text color for the given variant, falling back
    // to fixed light/dark values if the resource is unavailable.
    private static IBrush ResolveBrush(ThemeVariant variant)
    {
        if (Application.Current is { } app
            && app.TryGetResource("TextFillColorPrimaryBrush", variant, out object? resource)
            && resource is IBrush brush)
            return brush;

        bool isDark = variant != ThemeVariant.Light;
        return new SolidColorBrush(isDark ? Color.FromRgb(0xE8, 0xE8, 0xE8) : Color.FromRgb(0x1A, 0x1A, 0x1A));
    }
}
