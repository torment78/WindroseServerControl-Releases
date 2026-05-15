using System.IO;
using System.Text.Json;
using Elka_windrose_server_control.Models;

namespace Elka_windrose_server_control.Services;

public static class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load(string settingsPath)
    {
        if (!File.Exists(settingsPath))
            return new AppSettings();

        string json = File.ReadAllText(settingsPath);

        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
               ?? new AppSettings();
    }

    public static void Save(string settingsPath, AppSettings settings)
    {
        string? folder = Path.GetDirectoryName(settingsPath);

        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(settingsPath, json);
    }
}