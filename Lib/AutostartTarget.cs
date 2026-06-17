namespace MiniMetrics.Lib;

// Chooses which executable path the run-at-startup entry should point at. A Velopack install runs from a
// versioned "current" folder that changes on every update, so it registers the stable root stub instead;
// a portable or dev build registers the running exe. Falls back to the running exe if no stub is known.
public static class AutostartTarget
{
    public static string Resolve(bool installed, string? rootStubPath, string processPath) =>
        installed && !string.IsNullOrEmpty(rootStubPath) ? rootStubPath : processPath;
}
