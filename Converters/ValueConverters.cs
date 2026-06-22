using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
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
        value is string name && !string.IsNullOrWhiteSpace(name) ? new(name) : FontFamily.Default;

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

// Compares an int index against the converter parameter, so a view can show one of several stacked
// panes by index (the selected rail item) without the view model tracking the selection.
public sealed class IndexEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index && parameter is string text && int.TryParse(text, out int target) && index == target;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Lights up a swatch's ring when its hex matches the currently selected background color. Bound as a
// multi-value converter over [swatch hex, selected hex] so the active swatch gets a 2px border and the
// rest get none, without the view model tracking a separate "selected swatch" field.
public sealed class SwatchSelectionThicknessConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not string swatch || values[1] is not string selected)
            return new Thickness(0);

        return string.Equals(swatch.Trim(), selected.Trim(), StringComparison.OrdinalIgnoreCase)
            ? new Thickness(2)
            : new Thickness(0);
    }
}
