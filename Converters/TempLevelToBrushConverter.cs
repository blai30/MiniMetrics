using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using MiniMetrics.Lib;

namespace MiniMetrics.Converters;

public sealed class TempLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isDark = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        TempLevel level = value is TempLevel tempLevel ? tempLevel : (TempLevel)(-1);
        return new SolidColorBrush(Color.Parse(ThemePalette.TempColor(level, isDark)));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
