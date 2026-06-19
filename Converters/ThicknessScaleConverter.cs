using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MiniMetrics.Converters;

// Scales a base Thickness (the converter parameter, e.g. "20,14,20,16") by the bound scale factor so
// margins and padding grow with the font. The parameter is parsed with the invariant culture because
// it comes from XAML.
public sealed class ThicknessScaleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double scale = value is double factor ? factor : 1.0;
        var baseThickness = parameter is string text
            ? Thickness.Parse(text)
            : default;
        return new Thickness(
            baseThickness.Left * scale,
            baseThickness.Top * scale,
            baseThickness.Right * scale,
            baseThickness.Bottom * scale);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
