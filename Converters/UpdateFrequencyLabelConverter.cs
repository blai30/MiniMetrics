using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MiniMetrics.Models;

namespace MiniMetrics.Converters;

// Renders a UpdateCheckFrequency enum value as a friendly label for the settings dropdown.
public sealed class UpdateFrequencyLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        UpdateCheckFrequency.EveryLaunch => "On every launch",
        UpdateCheckFrequency.Daily => "Daily",
        UpdateCheckFrequency.Weekly => "Weekly",
        UpdateCheckFrequency.Monthly => "Monthly",
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
