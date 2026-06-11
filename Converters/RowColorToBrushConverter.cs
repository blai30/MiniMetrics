using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DesktopMetrics.Lib;

namespace DesktopMetrics.Converters;

public sealed class RowColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        (string from, string to) = value switch
        {
            RowColor.Cyan => ("#38BDF8", "#22D3EE"),
            RowColor.Green => ("#34D399", "#10B981"),
            RowColor.Amber => ("#FBBF24", "#F59E0B"),
            RowColor.Violet => ("#A78BFA", "#8B5CF6"),
            _ => ("#38BDF8", "#22D3EE"),
        };

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse(from), 0),
                new GradientStop(Color.Parse(to), 1),
            },
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
