namespace MiniMetrics.Services;

// Off Windows the PawnIO driver does not exist, so detection always reports false and the elevation
// gate never relaunches.
public sealed class NoopDriverProbe : IDriverProbe
{
    public bool IsInstalled() => false;
}
