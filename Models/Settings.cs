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
    public string? TimeZoneId { get; set; }
    public Dictionary<string, bool> Visibility { get; set; } = new();
}
