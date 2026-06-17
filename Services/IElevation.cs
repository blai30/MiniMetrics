namespace MiniMetrics.Services;

// The OS seam for runtime elevation. WindowsElevation is the real implementation; NoopElevation
// stands in off Windows so the launch gate and the runtime toggle never try to relaunch there.
public interface IElevation
{
    // True when the current process holds an administrator token.
    bool IsElevated();

    // Starts a new elevated copy of the app through the UAC prompt. Returns false if the prompt was
    // declined or the relaunch could not start.
    bool RelaunchElevated(string exePath);
}
