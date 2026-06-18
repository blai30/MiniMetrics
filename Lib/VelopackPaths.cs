using System.IO;

namespace MiniMetrics.Lib;

// Resolves the path to Velopack's Update.exe relative to the running app. An installed Velopack app runs
// from a versioned "current" folder and Update.exe lives in its parent (the install root). The parent is
// computed by normalizing "<base>\.." rather than Directory.GetParent: AppContext.BaseDirectory always ends
// in a separator, and Directory.GetParent returns the "current" folder itself for a trailing-separator path,
// which would point Process.Start at a non-existent Update.exe and silently abort the uninstall.
public static class VelopackPaths
{
    public static string ResolveUpdateExe(string baseDirectory)
    {
        string root = Path.GetFullPath(Path.Combine(baseDirectory, ".."));
        return Path.Combine(root, "Update.exe");
    }
}
