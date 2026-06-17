namespace MiniMetrics.Services;

// Off Windows there is no UAC and no PawnIO driver. Reporting "already elevated" makes both the launch
// gate and the runtime toggle skip relaunching entirely.
public sealed class NoopElevation : IElevation
{
    public bool IsElevated() => true;

    public bool RelaunchElevated(string exePath) => false;
}
