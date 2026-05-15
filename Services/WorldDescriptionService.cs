using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elka_windrose_server_control.Services
{
    public static class WorldDescriptionService
    {
        public const string ElkaWorldProfilesFolderName = "ElkaWorldProfiles";
        public const string DefaultWorldDescriptionFileName = "WorldDescription.default.json";
        public const string WorldDescriptionFileName = "WorldDescription.json";

        public static string GetWorldDescriptionPath(string worldFolder)
        {
            return Path.Combine(worldFolder, WorldDescriptionFileName);
        }

        public static string GetElkaWorldProfilesFolder(string worldFolder)
        {
            return Path.Combine(worldFolder, ElkaWorldProfilesFolderName);
        }

        public static string GetDefaultWorldDescriptionPath(string worldFolder)
        {
            return Path.Combine(
                GetElkaWorldProfilesFolder(worldFolder),
                DefaultWorldDescriptionFileName
            );
        }

        public static string GetProfileFolder(string worldFolder, string profileName)
        {
            string safeProfileName = MakeSafeFolderName(profileName);

            return Path.Combine(
                GetElkaWorldProfilesFolder(worldFolder),
                safeProfileName
            );
        }

        public static string GetProfileWorldDescriptionPath(string worldFolder, string profileName)
        {
            return Path.Combine(
                GetProfileFolder(worldFolder, profileName),
                WorldDescriptionFileName
            );
        }

        public static bool HasWorldDescription(string worldFolder)
        {
            return File.Exists(GetWorldDescriptionPath(worldFolder));
        }

        public static void EnsureDefaultBackup(string worldFolder)
        {
            string worldDescriptionPath = GetWorldDescriptionPath(worldFolder);

            if (!File.Exists(worldDescriptionPath))
                throw new FileNotFoundException("WorldDescription.json was not found.", worldDescriptionPath);

            string profilesFolder = GetElkaWorldProfilesFolder(worldFolder);
            Directory.CreateDirectory(profilesFolder);

            string defaultPath = GetDefaultWorldDescriptionPath(worldFolder);

            if (File.Exists(defaultPath))
                return;

            File.Copy(
                worldDescriptionPath,
                defaultPath,
                overwrite: false
            );
        }

        public static void RestoreDefaultToWorld(string worldFolder)
        {
            string defaultPath = GetDefaultWorldDescriptionPath(worldFolder);

            if (!File.Exists(defaultPath))
                throw new FileNotFoundException("Default WorldDescription backup was not found.", defaultPath);

            File.Copy(
                defaultPath,
                GetWorldDescriptionPath(worldFolder),
                overwrite: true
            );
        }

        public static void SaveCurrentWorldDescriptionAsProfile(string worldFolder, string profileName)
        {
            EnsureDefaultBackup(worldFolder);

            string sourcePath = GetWorldDescriptionPath(worldFolder);
            string profileFolder = GetProfileFolder(worldFolder, profileName);
            Directory.CreateDirectory(profileFolder);

            File.Copy(
                sourcePath,
                GetProfileWorldDescriptionPath(worldFolder, profileName),
                overwrite: true
            );
        }

        public static void ApplyProfileToWorld(string worldFolder, string profileName)
        {
            string profilePath = GetProfileWorldDescriptionPath(worldFolder, profileName);

            if (!File.Exists(profilePath))
                throw new FileNotFoundException("Profile WorldDescription.json was not found.", profilePath);

            File.Copy(
                profilePath,
                GetWorldDescriptionPath(worldFolder),
                overwrite: true
            );
        }

        public static JsonObject LoadWorldDescription(string path)
        {
            string json = File.ReadAllText(path);

            JsonNode? node = JsonNode.Parse(json);

            if (node is not JsonObject obj)
                throw new InvalidOperationException("WorldDescription.json root was not a JSON object.");

            return obj;
        }

        public static void SaveWorldDescription(string path, JsonObject root)
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            string json = root.ToJsonString(options);
            File.WriteAllText(path, json);
        }

        public static JsonObject BuildPresetFromCurrentWorld(
            string worldFolder,
            string presetType,
            string combatDifficulty,
            bool sharedQuests,
            bool immersiveExploration,
            double enemyHealthPercent,
            double enemyDamagePercent,
            double enemyShipHealthPercent,
            double enemyShipDamagePercent,
            double boardingDifficultyPercent,
            double coopEnemyScalingPercent,
            double coopShipScalingPercent)
        {
            string worldDescriptionPath = GetWorldDescriptionPath(worldFolder);

            if (!File.Exists(worldDescriptionPath))
                throw new FileNotFoundException("WorldDescription.json was not found.", worldDescriptionPath);

            JsonObject root = LoadWorldDescription(worldDescriptionPath);

            JsonObject worldDescription = GetRequiredObject(root, "WorldDescription");
            JsonObject worldSettings = GetRequiredObject(worldDescription, "WorldSettings");
            JsonObject boolParameters = GetRequiredObject(worldSettings, "BoolParameters");
            JsonObject floatParameters = GetRequiredObject(worldSettings, "FloatParameters");
            JsonObject tagParameters = GetRequiredObject(worldSettings, "TagParameters");

            worldDescription["WorldPresetType"] = presetType;

            boolParameters[Key("WDS.Parameter.Coop.SharedQuests")] = sharedQuests;
            boolParameters[Key("WDS.Parameter.EasyExplore")] = immersiveExploration;

            floatParameters[Key("WDS.Parameter.MobHealthMultiplier")] = PercentToJsonValue(enemyHealthPercent);
            floatParameters[Key("WDS.Parameter.MobDamageMultiplier")] = PercentToJsonValue(enemyDamagePercent);
            floatParameters[Key("WDS.Parameter.ShipsHealthMultiplier")] = PercentToJsonValue(enemyShipHealthPercent);
            floatParameters[Key("WDS.Parameter.ShipsDamageMultiplier")] = PercentToJsonValue(enemyShipDamagePercent);
            floatParameters[Key("WDS.Parameter.BoardingDifficultyMultiplier")] = PercentToJsonValue(boardingDifficultyPercent);
            floatParameters[Key("WDS.Parameter.Coop.StatsCorrectionModifier")] = PercentToJsonValue(coopEnemyScalingPercent);
            floatParameters[Key("WDS.Parameter.Coop.ShipStatsCorrectionModifier")] = PercentToJsonValue(coopShipScalingPercent);

            tagParameters[Key("WDS.Parameter.CombatDifficulty")] = new JsonObject
            {
                ["TagName"] = $"WDS.Parameter.CombatDifficulty.{combatDifficulty}"
            };

            return root;
        }

        public static void SavePresetToWorldProfile(
            string worldFolder,
            string profileName,
            JsonObject presetRoot)
        {
            EnsureDefaultBackup(worldFolder);

            string profileFolder = GetProfileFolder(worldFolder, profileName);
            Directory.CreateDirectory(profileFolder);

            SaveWorldDescription(
                GetProfileWorldDescriptionPath(worldFolder, profileName),
                presetRoot
            );
        }

        private static JsonObject GetRequiredObject(JsonObject parent, string propertyName)
        {
            if (parent[propertyName] is JsonObject obj)
                return obj;

            throw new InvalidOperationException($"Missing JSON object: {propertyName}");
        }

        private static double PercentToJsonValue(double percent)
        {
            return Math.Round(percent / 100.0, 4);
        }

        private static string Key(string tagName)
        {
            return $"{{\"TagName\": \"{tagName}\"}}";
        }

        private static string MakeSafeFolderName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Trim();
        }
    }
}