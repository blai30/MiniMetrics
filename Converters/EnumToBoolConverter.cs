using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MiniMetrics.Converters;

// Maps an enum value to a bool by comparing it against the converter parameter. Used to bind a group
// of radio buttons to a single enum property: each radio passes its own enum value as the parameter.
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.Equals(parameter) ?? false;

    // Only the radio being checked writes back; an uncheck leaves the source untouched so the group
    // never clears its selection.
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : BindingOperations.DoNothing;
}
