using System;
using System.IO;
using System.Text.Json;
using MiniMetrics.Models;

namespace MiniMetrics.Services;

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    // %APPDATA%\MiniMetrics\settings.json
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MiniMetrics",
        "settings.json");

    public Settings Load()
    {
        try
        {
            if (!File.Exists(path)) return new();

            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new();
        }
        catch
        {
            // A corrupt or unreadable file would otherwise silently reset every setting. Preserve it as
            // .bak so the user's config is recoverable and the failure is diagnosable, then fall back to
            // defaults rather than crashing.
            TryBackup();
            return new();
        }
    }

    private void TryBackup()
    {
        try
        {
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        }
        catch
        {
            // Best effort; never let a backup failure crash startup.
        }
    }

    public void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Persistence is best effort; a failure to write settings must not crash the widget.
        }
    }
}
