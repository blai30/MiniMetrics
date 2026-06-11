using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DesktopMetrics.Lib;

namespace DesktopMetrics.Converters;

public sealed class TempLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string hex = value switch
        {
            TempLevel.Cool => "#5ED6A8",
            TempLevel.Warm => "#F5B544",
            TempLevel.Hot => "#F87171",
            _ => "#9AA1AC",
        };

        return new SolidColorBrush(Color.Parse(hex));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
