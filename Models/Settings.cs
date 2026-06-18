using System;
using System.Collections.Generic;

namespace MiniMetrics.Models;

public sealed class Settings
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool Locked { get; set; }
    public bool Hidden { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool SnapToEdges { get; set; } = true;
    public string BackgroundColor { get; set; } = "#0F121D";
    public int Opacity { get; set; } = 96;
    public int? DateTimeX { get; set; }
    public int? DateTimeY { get; set; }
    public bool DateTimeHidden { get; set; } = true;
    public int? GpuX { get; set; }
    public int? GpuY { get; set; }
    public bool GpuHidden { get; set; }
    public string? TimeZoneId { get; set; }
    public string? ClockLocaleId { get; set; }
    public string? ClockTimeFormat { get; set; }
    public string? ClockDateFormat { get; set; }
    public string? ClockTimeFormatHover { get; set; }
    public string? ClockDateFormatHover { get; set; }
    public Dictionary<string, bool> Visibility { get; set; } = new();
    public bool UpdateCheckEnabled { get; set; } = true;
    public UpdateCheckFrequency UpdateFrequency { get; set; } = UpdateCheckFrequency.Daily;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public string? SkippedUpdateVersion { get; set; }
}
