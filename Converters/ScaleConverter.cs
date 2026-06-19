using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MiniMetrics.Converters;

// Multiplies a base scalar (a font size, spacing, or min-width given as the converter parameter) by the
// bound scale factor. Scaling the real font size, rather than applying a render transform, keeps text
// crisp at every scale. The parameter is parsed with the invariant culture because it comes from XAML.
public sealed class ScaleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double scale = value is double factor ? factor : 1.0;
        double baseValue = parameter is string text
            ? double.Parse(text, CultureInfo.InvariantCulture)
            : 0.0;
        return baseValue * scale;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
