using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniMetrics.Lib;

namespace MiniMetrics.ViewModels;

public partial class DateTimeWidgetViewModel : ObservableObject
{
    private DateTimeOffset _instant;
    private TimeZoneInfo _zone = TimeZoneInfo.Local;

    [ObservableProperty]
    private string _timeText = "";

    [ObservableProperty]
    private string _dateText = "";

    // Flipped by the view while the pointer hovers the widget; reformats immediately.
    [ObservableProperty]
    private bool _is24Hour;

    [ObservableProperty]
    private IBrush _cardBackground = Brushes.Transparent;

    // Recomputes the card's solid background color from a base color and opacity (shared with the
    // metrics widget's appearance logic).
    public void ApplyAppearance(string backgroundColor, int opacity)
    {
        string color = AppearanceColor.Derive(backgroundColor, opacity);
        CardBackground = new SolidColorBrush(Color.Parse(color));
    }

    // Updates the active time zone and reformats the last known instant.
    public void SetTimeZone(TimeZoneInfo zone)
    {
        _zone = zone;
        Refresh();
    }

    // Advances the clock to a new instant (called once per second by App) and reformats.
    public void Tick(DateTimeOffset instant)
    {
        _instant = instant;
        Refresh();
    }

    partial void OnIs24HourChanged(bool value) => Refresh();

    private void Refresh()
    {
        TimeText = ClockFormatting.FormatTime(_instant, _zone, Is24Hour);
        DateText = ClockFormatting.FormatDate(_instant, _zone, showZone: Is24Hour);
    }
}
