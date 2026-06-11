using System;
using System.IO;
using System.Text.Json;
using DesktopMetrics.Models;

namespace DesktopMetrics.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    // %APPDATA%\DesktopMetrics\settings.json
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopMetrics",
        "settings.json");

    public Settings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new Settings();
            }

            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    public void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Persistence is best effort; a failure to write settings must not crash the widget.
        }
    }
}
