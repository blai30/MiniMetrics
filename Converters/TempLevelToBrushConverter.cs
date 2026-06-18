using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MiniMetrics.Lib;

namespace MiniMetrics.Converters;

public sealed class TempLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string hex = value switch
        {
            TempLevel.Frigid => "#7DD3FC",
            TempLevel.Cold => "#2DD4BF",
            TempLevel.Cool => "#5ED6A8",
            TempLevel.Warm => "#F5B544",
            TempLevel.Hot => "#FB923C",
            TempLevel.Critical => "#F87171",
            _ => "#9AA1AC",
        };

        return new SolidColorBrush(Color.Parse(hex));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
