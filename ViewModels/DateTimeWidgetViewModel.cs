using System;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.ViewModels;

public partial class DateTimeWidgetViewModel : ObservableObject, IWidgetDisplay, ICompactWidget
{
    private DateTimeOffset _instant;
    private TimeZoneInfo _zone = TimeZoneInfo.Local;
    private CultureInfo _culture = CultureInfo.CurrentCulture;
    private string? _timeFormat;
    private string? _dateFormat;
    private string? _timeFormatHover;
    private string? _dateFormatHover;

    [ObservableProperty] public partial string TimeText { get; set; } = "";
    [ObservableProperty] public partial string DateText { get; set; } = "";

    // Flipped by the view while the pointer hovers the widget; swaps the normal format pair for the
    // hover pair and reformats immediately.
    [ObservableProperty] public partial bool IsHovering { get; set; }
    [ObservableProperty] public partial IBrush CardBackground { get; set; } = Brushes.Transparent;

    // Drives the clock window between its stacked layout and the single-line compact layout.
    [ObservableProperty] public partial bool IsCompact { get; set; }

    // Drives the horizontal alignment of the clock text. TextAlignment positions the time and date
    // lines in the fixed-width full layout; BlockAlignment is applied to the compact layout's inline
    // row (a no-op while that window hugs its content, kept for consistency).
    [ObservableProperty] public partial TextAlignment TextAlignment { get; set; } = TextAlignment.Left;
    [ObservableProperty] public partial HorizontalAlignment BlockAlignment { get; set; } = HorizontalAlignment.Left;

    private const double BaseWidth = 640;
    private const double BaseHeight = 176;
    private ClockWidthMode _widthMode = ClockWidthMode.Auto;
    private double _customWidth = BaseWidth;

    [ObservableProperty] public partial FontFamily FontFamily { get; set; } = new(WidgetStyleProfile.BundledInter);
    [ObservableProperty] public partial double Scale { get; set; } = 1.0;
    [ObservableProperty] public partial double ScaledWidth { get; set; } = BaseWidth;
    [ObservableProperty] public partial double ScaledHeight { get; set; } = BaseHeight;
    [ObservableProperty] public partial FontWeight ClockWeight { get; set; } = FontWeight.Medium;

    // Recomputes the card's solid background color from a base color and opacity (shared with the
    // metrics widget's appearance logic).
    public void ApplyAppearance(string backgroundColor, int opacity)
    {
        string color = AppearanceColor.Derive(backgroundColor, opacity);
        CardBackground = new SolidColorBrush(Color.Parse(color));
    }

    public void ApplyStyle(WidgetStyleProfile profile)
    {
        FontFamily = new(profile.FontFamily);
        Scale = profile.Scale;
        ScaledWidth = ResolveWidth(profile.Scale);
        ScaledHeight = BaseHeight * profile.Scale;
        ClockWeight = (FontWeight)profile.ClockWeight;
    }

    // Updates the width adjustment mode and optionally a custom width. In Auto mode the widget resizes
    // to fit its content; in Fixed mode it uses the custom width scaled by the current widget scale.
    public void SetWidthMode(ClockWidthMode mode, int customWidth = 640)
    {
        _widthMode = mode;
        _customWidth = customWidth;
        ScaledWidth = ResolveWidth(Scale);
    }

    private double ResolveWidth(double scale) =>
        _widthMode == ClockWidthMode.Fixed ? _customWidth * scale : BaseWidth * scale;

    // Updates the active time zone and reformats the last known instant.
    public void SetTimeZone(TimeZoneInfo zone)
    {
        _zone = zone;
        Refresh();
    }

    // Updates the culture used to render every line and reformats.
    public void SetLocale(CultureInfo culture)
    {
        _culture = culture;
        Refresh();
    }

    // Updates the four custom format strings (null or blank means use the built-in default for that
    // line) and reformats.
    public void SetFormats(string? timeFormat, string? dateFormat, string? timeFormatHover, string? dateFormatHover)
    {
        _timeFormat = timeFormat;
        _dateFormat = dateFormat;
        _timeFormatHover = timeFormatHover;
        _dateFormatHover = dateFormatHover;
        Refresh();
    }

    // Maps the saved alignment choice onto the two layout-facing properties.
    public void SetAlignment(ClockAlignment alignment)
    {
        (TextAlignment, BlockAlignment) = alignment switch
        {
            ClockAlignment.Center => (TextAlignment.Center, HorizontalAlignment.Center),
            ClockAlignment.Right => (TextAlignment.Right, HorizontalAlignment.Right),
            _ => (TextAlignment.Left, HorizontalAlignment.Left)
        };
    }

    // Advances the clock to a new instant (called once per second by App) and reformats.
    public void Tick(DateTimeOffset instant)
    {
        _instant = instant;
        Refresh();
    }

    partial void OnIsHoveringChanged(bool value) => Refresh();

    private void Refresh()
    {
        (string? timeCustom, string timeDefault, string? dateCustom, string dateDefault) = IsHovering
            ? (_timeFormatHover, ClockFormatting.DefaultTimeFormatHover, _dateFormatHover,
                ClockFormatting.DefaultDateFormatHover)
            : (_timeFormat, ClockFormatting.DefaultTimeFormat, _dateFormat, ClockFormatting.DefaultDateFormat);

        TimeText = ClockFormatting.Render(_instant, _zone, timeCustom, timeDefault, _culture);
        DateText = ClockFormatting.Render(_instant, _zone, dateCustom, dateDefault, _culture);
    }
}
