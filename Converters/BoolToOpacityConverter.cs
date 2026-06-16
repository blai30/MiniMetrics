using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MiniMetrics.Converters;

// Maps a visibility flag to an opacity. Returning 0 instead of collapsing the control keeps its
// layout slot, so hiding one metric inside a card leaves the others exactly where they were.
public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 1.0 : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
