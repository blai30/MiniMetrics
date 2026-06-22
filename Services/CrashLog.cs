using System;
using System.IO;
using System.Reflection;

namespace MiniMetrics.Services;

// Last-resort diagnostic sink. A field crash on a clean machine otherwise leaves no trace, so this
// appends unhandled exceptions to %APPDATA%\MiniMetrics\logs\crash.log. Every write is best effort:
// the logger must never throw, or it would mask the original failure.
public static class CrashLog
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MiniMetrics",
        "logs");

    public static void Write(string source, Exception? exception)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            string entry =
                $"[{DateTimeOffset.UtcNow:u}] v{version} {source}{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(Directory, "crash.log"), entry);
        }
        catch
        {
            // Best effort; never let logging crash the process.
        }
    }
}
