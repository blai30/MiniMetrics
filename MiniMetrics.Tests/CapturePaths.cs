namespace MiniMetrics.Tests;

public static class CapturePaths
{
    // Walks up from the test binary location to the repo root (the directory holding the solution file)
    // and returns its captures/ subdirectory, creating it if needed. Falls back to the binary directory
    // if the solution file is not found.
    public static string OutputDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MiniMetrics.slnx")))
            directory = directory.Parent;

        string root = directory?.FullName ?? AppContext.BaseDirectory;
        string captures = Path.Combine(root, "captures");
        Directory.CreateDirectory(captures);
        return captures;
    }
}
