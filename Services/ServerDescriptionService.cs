using System.IO;
using System.Text.Json;
using Elka_windrose_server_control.Models;

namespace Elka_windrose_server_control.Services;

public static class ServerDescriptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static ServerDescriptionRoot Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("Server description JSON was not found.", jsonPath);

        string json = File.ReadAllText(jsonPath);

        ServerDescriptionRoot? root = JsonSerializer.Deserialize<ServerDescriptionRoot>(json, JsonOptions);

        if (root == null)
            throw new InvalidOperationException("Server description JSON could not be loaded.");

        root.ServerDescription_Persistent ??= new ServerDescriptionPersistent();

        return root;
    }

    public static void Backup(string jsonPath, string backupsRoot)
    {
        if (!File.Exists(jsonPath))
            return;

        Directory.CreateDirectory(backupsRoot);

        string backupFileName = $"server description_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
        string backupPath = Path.Combine(backupsRoot, backupFileName);

        File.Copy(jsonPath, backupPath, overwrite: false);
    }

    public static void Save(string jsonPath, ServerDescriptionRoot root)
    {
        string json = JsonSerializer.Serialize(root, JsonOptions);
        File.WriteAllText(jsonPath, json);
    }
}