using System.IO;

namespace Elka_windrose_server_control.Services;

public sealed class WorldFolderInfo
{
    public string WorldId { get; init; } = "";
    public string FullPath { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
}

public static class WorldImportService
{
    public static string GetPlayerSaveProfilesRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "R5",
            "Saved",
            "SaveProfiles"
        );
    }

    public static List<WorldFolderInfo> FindPlayerWorlds()
    {
        string saveProfilesRoot = GetPlayerSaveProfilesRoot();

        if (!Directory.Exists(saveProfilesRoot))
            return [];

        List<WorldFolderInfo> worlds = [];

        foreach (string steamIdFolder in Directory.GetDirectories(saveProfilesRoot))
        {
            string rocksDbFolder = Path.Combine(steamIdFolder, "RocksDB");

            if (!Directory.Exists(rocksDbFolder))
                continue;

            string? versionFolder = GetHighestVersionFolder(rocksDbFolder);

            if (versionFolder == null)
                continue;

            string worldsFolder = Path.Combine(versionFolder, "Worlds");

            if (!Directory.Exists(worldsFolder))
                continue;

            foreach (string worldFolder in Directory.GetDirectories(worldsFolder))
            {
                worlds.Add(new WorldFolderInfo
                {
                    WorldId = Path.GetFileName(worldFolder),
                    FullPath = worldFolder,
                    CreatedUtc = Directory.GetCreationTimeUtc(worldFolder)
                });
            }
        }

        return worlds
            .OrderByDescending(x => x.CreatedUtc)
            .ToList();
    }

    public static string GetServerWorldsFolder(string serverFolder)
    {
        List<string> worldRoots = FindServerWorldRoots(serverFolder);

        if (worldRoots.Count > 0)
            return worldRoots[0];

        return Path.Combine(
            serverFolder,
            "R5",
            "Saved",
            "SaveProfiles",
            "0",
            "RocksDB_V2",
            "010",
            "Worlds"
        );
    }
    public static List<string> FindServerWorldRoots(string serverFolder)
    {
        string saveProfilesRoot = Path.Combine(
            serverFolder,
            "R5",
            "Saved",
            "SaveProfiles"
        );

        if (!Directory.Exists(saveProfilesRoot))
            return new List<string>();

        return Directory
            .GetDirectories(saveProfilesRoot, "Worlds", SearchOption.AllDirectories)
            .Where(path => path.Contains("RocksDB", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(GetRocksDbVersionScore)
            .ThenByDescending(GetSaveVersionScore)
            .ThenByDescending(Directory.GetLastWriteTimeUtc)
            .ToList();
    }

    private static int GetRocksDbVersionScore(string path)
    {
        string[] parts = path.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );

        string? rocksPart = parts.FirstOrDefault(part =>
            part.StartsWith("RocksDB", StringComparison.OrdinalIgnoreCase));

        if (rocksPart == null)
            return 0;

        if (rocksPart.Equals("RocksDB", StringComparison.OrdinalIgnoreCase))
            return 1;

        int underscoreIndex = rocksPart.LastIndexOf("_V", StringComparison.OrdinalIgnoreCase);

        if (underscoreIndex < 0)
            return 1;

        string numberText = rocksPart[(underscoreIndex + 2)..];

        if (int.TryParse(numberText, out int version))
            return 100 + version;

        return 1;
    }

    private static int GetSaveVersionScore(string path)
    {
        string[] parts = path.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );

        for (int i = 0; i < parts.Length; i++)
        {
            if (!parts[i].StartsWith("RocksDB", StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= parts.Length)
                return 0;

            string versionFolder = parts[i + 1];

            string numericOnly = new string(versionFolder.Where(char.IsDigit).ToArray());

            if (int.TryParse(numericOnly, out int version))
                return version;

            return 0;
        }

        return 0;
    }
    public static void MigrateWorldsToNewestRoot(string serverFolder, Action<string>? log = null)
    {
        List<string> roots = FindServerWorldRoots(serverFolder);

        if (roots.Count <= 1)
            return;

        string targetRoot = roots[0];

        Directory.CreateDirectory(targetRoot);

        foreach (string sourceRoot in roots.Skip(1))
        {
            if (!Directory.Exists(sourceRoot))
                continue;

            foreach (string sourceWorldFolder in Directory.GetDirectories(sourceRoot))
            {
                string worldName = Path.GetFileName(sourceWorldFolder);
                string targetWorldFolder = Path.Combine(targetRoot, worldName);

                if (Directory.Exists(targetWorldFolder))
                {
                    log?.Invoke($"World already exists in newest world folder, skipped: {worldName}");
                    continue;
                }

                CopyDirectory(sourceWorldFolder, targetWorldFolder);

                log?.Invoke($"Migrated world to newest world folder: {worldName}");
            }
        }
    }
    private static int GetRocksDbPreferenceScore(string path)
    {
        if (path.Contains("RocksDB_V3", StringComparison.OrdinalIgnoreCase))
            return 3;

        if (path.Contains("RocksDB_V2", StringComparison.OrdinalIgnoreCase))
            return 2;

        if (path.Contains("RocksDB", StringComparison.OrdinalIgnoreCase))
            return 1;

        return 0;
    }

    public static List<WorldFolderInfo> FindImportedServerWorlds(string copiedServerFolder)
    {
        string serverWorldsFolder = GetServerWorldsFolder(copiedServerFolder);

        if (!Directory.Exists(serverWorldsFolder))
            return [];

        return Directory.GetDirectories(serverWorldsFolder)
            .Select(folder => new WorldFolderInfo
            {
                WorldId = Path.GetFileName(folder),
                FullPath = folder,
                CreatedUtc = Directory.GetCreationTimeUtc(folder)
            })
            .OrderByDescending(x => x.CreatedUtc)
            .ToList();
    }

    public static void CopyWorldToServer(WorldFolderInfo sourceWorld, string copiedServerFolder)
    {
        string serverWorldsFolder = GetServerWorldsFolder(copiedServerFolder);
        Directory.CreateDirectory(serverWorldsFolder);

        string destinationFolder = Path.Combine(serverWorldsFolder, sourceWorld.WorldId);

        if (Directory.Exists(destinationFolder))
        {
            // Do not overwrite or re-date an already imported world.
            return;
        }

        CopyDirectory(sourceWorld.FullPath, destinationFolder);
    }

    private static string? GetHighestVersionFolder(string parentFolder)
    {
        if (!Directory.Exists(parentFolder))
            return null;

        return Directory.GetDirectories(parentFolder)
            .OrderByDescending(path =>
            {
                string name = Path.GetFileName(path);

                if (Version.TryParse(name, out Version? version))
                    return version;

                return new Version(0, 0, 0);
            })
            .FirstOrDefault();
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destinationFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (string directory in Directory.GetDirectories(sourceDir))
        {
            string destinationSubDir = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, destinationSubDir);
        }
    }

    public static void CopyWorldFolderToServer(string sourceWorldFolder, string serverFolder)
    {
        string worldsFolder = GetServerWorldsFolder(serverFolder);

        Directory.CreateDirectory(worldsFolder);

        string targetWorldFolder = Path.Combine(
            worldsFolder,
            Path.GetFileName(sourceWorldFolder)
        );

        CopyDirectory(sourceWorldFolder, targetWorldFolder);
    }
}