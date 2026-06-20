using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using MiniMetrics.Lib;
using MiniMetrics.Models;

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

public sealed class RowColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isDark = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        var color = value is RowColor rowColor ? rowColor : RowColor.Cyan;
        (string from, string to) = ThemePalette.BarGradient(color, isDark);

        return new LinearGradientBrush
        {
            StartPoint = new(0, 0, RelativeUnit.Relative),
            EndPoint = new(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new(Color.Parse(from), 0),
                new(Color.Parse(to), 1)
            }
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class TempLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isDark = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        var level = value is TempLevel tempLevel ? tempLevel : (TempLevel)(-1);
        return new SolidColorBrush(Color.Parse(ThemePalette.TempColor(level, isDark)));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Turns a font-family name into a FontFamily so a list item can be drawn in the font it names. Compiled
// bindings do not run the implicit string-to-FontFamily conversion, so binding FontFamily to a raw string
// silently falls back to the default font; this makes the conversion explicit.
public sealed class StringToFontFamilyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string name && !string.IsNullOrWhiteSpace(name) ? new FontFamily(name) : FontFamily.Default;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Renders a UpdateCheckFrequency enum value as a friendly label for the settings dropdown.
public sealed class UpdateFrequencyLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        UpdateCheckFrequency.EveryLaunch => "On every launch",
        UpdateCheckFrequency.Daily => "Daily",
        UpdateCheckFrequency.Weekly => "Weekly",
        UpdateCheckFrequency.Monthly => "Monthly",
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
