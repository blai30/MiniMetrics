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
            return new();
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
