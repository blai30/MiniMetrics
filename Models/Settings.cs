using System.Collections.Generic;

namespace DesktopMetrics.Models;

public sealed class Settings
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool Locked { get; set; }
    public bool Hidden { get; set; }
    public string BackgroundColor { get; set; } = "#0F121D";
    public int Opacity { get; set; } = 96;
    public Dictionary<string, bool> Visibility { get; set; } = new();
}
