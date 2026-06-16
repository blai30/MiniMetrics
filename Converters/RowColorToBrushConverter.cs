using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MiniMetrics.Lib;

namespace MiniMetrics.Converters;

public sealed class RowColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Each pair runs dark -> light so the bar gradient brightens left to right.
        (string from, string to) = value switch
        {
            RowColor.Cyan => ("#0EA5E9", "#67E8F9"),
            RowColor.Green => ("#10B981", "#6EE7B7"),
            RowColor.Amber => ("#F59E0B", "#FCD34D"),
            RowColor.Violet => ("#8B5CF6", "#C4B5FD"),
            _ => ("#0EA5E9", "#67E8F9"),
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
