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
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string LightBackgroundColor { get; set; } = "#EEF1F5";
    public int Opacity { get; set; } = 96;
    public bool CpuCompact { get; set; }
    public bool GpuCompact { get; set; }
    public bool DateTimeCompact { get; set; }
    public string? WidgetFontFamily { get; set; }
    public int WidgetScale { get; set; } = 100;
    public WidgetFontWeight WidgetFontWeight { get; set; } = WidgetFontWeight.Regular;
    public ClockAlignment ClockAlignment { get; set; } = ClockAlignment.Left;
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
    public Dictionary<string, bool> Visibility { get; set; } = [];
    public bool UpdateCheckEnabled { get; set; } = true;
    public UpdateCheckFrequency UpdateFrequency { get; set; } = UpdateCheckFrequency.Daily;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public string? SkippedUpdateVersion { get; set; }
}
