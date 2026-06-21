using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MiniMetrics.Converters;

// Maps a metric key (e.g. "cpu.usage") to its Lucide glyph for the Metrics settings rows. The
// converter owns these four geometries directly: a binding cannot select a StaticResource, and a
// value converter cannot reach the window's merged resource dictionary. The metric icons appear
// nowhere else, so there is no duplication with Views/LucideIcons.axaml.
public sealed class MetricIconConverter : IValueConverter
{
    private static readonly Geometry Activity =
        StreamGeometry.Parse(
            "M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2");

    private static readonly Geometry Thermometer =
        StreamGeometry.Parse("M14 4v10.54a4 4 0 1 1-4 0V4a2 2 0 0 1 4 0Z");

    private static readonly Geometry Zap =
        StreamGeometry.Parse(
            "M4 14a1 1 0 0 1-.78-1.63l9.9-10.2a.5.5 0 0 1 .86.46l-1.92 6.02A1 1 0 0 0 13 10h7a1 1 0 0 1 .78 1.63l-9.9 10.2a.5.5 0 0 1-.86-.46l1.92-6.02A1 1 0 0 0 11 14z");

    private static readonly Geometry MemoryStick =
        StreamGeometry.Parse(
            "M12 12v-2 M12 18v-2 M16 12v-2 M16 18v-2 M2 11h1.5 M20 18v-2 M20.5 11H22 M4 18v-2 M8 12v-2 M8 18v-2 M4 6h16a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2h-16a2 2 0 0 1-2-2v-6a2 2 0 0 1 2-2z");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key
            ? key switch
            {
                "cpu.usage" or "gpu.usage" => Activity,
                "cpu.temp" or "gpu.temp" => Thermometer,
                "cpu.power" or "gpu.power" => Zap,
                "ram.usage" or "vram.usage" => MemoryStick,
                _ => null
            }
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
