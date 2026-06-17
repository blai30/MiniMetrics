namespace MiniMetrics.Services;

// The OS seam for detecting the PawnIO kernel driver. LibreHardwareMonitor reads CPU package
// temperature and power through PawnIO, so without it those metrics are unavailable no matter how the
// process is elevated. WindowsDriverProbe is the real check; NoopDriverProbe stands in off Windows,
// where the driver never applies.
public interface IDriverProbe
{
    // True when the PawnIO driver is installed on this machine. Elevation can read CPU temperature and
    // power only when this is true.
    bool IsInstalled();
}
