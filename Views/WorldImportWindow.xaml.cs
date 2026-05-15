using Elka_windrose_server_control.Services;
using System.IO;
using System.Text.Json;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace Elka_windrose_server_control.Views;

public sealed class ImportableWorldItem
{
    public string DisplayName { get; set; } = "";
    public string FolderPath { get; set; } = "";

    public override string ToString()
    {
        return DisplayName;
    }
}


public partial class WorldImportWindow : Window
{
    private readonly string _serverFolder;
    private List<WorldFolderInfo> _availablePlayerWorlds = [];

    public bool WorldsChanged { get; private set; }

    public WorldImportWindow(string serverFolder)
    {
        InitializeComponent();

        _serverFolder = serverFolder;
    }
    private List<ImportableWorldItem> FindWorldFoldersInside(string pickedFolder)
    {
        List<ImportableWorldItem> worlds = new();

        if (!Directory.Exists(pickedFolder))
            return worlds;

        // Case 1:
        // user picked the exact world folder
        if (File.Exists(Path.Combine(pickedFolder, "WorldDescription.json")))
        {
            worlds.Add(CreateImportableWorldItem(pickedFolder));
            return worlds;
        }

        // Case 2:
        // user picked Worlds folder, RocksDB folder, SaveProfiles folder, desktop folder, etc.
        foreach (string worldDescriptionPath in Directory.GetFiles(
                     pickedFolder,
                     "WorldDescription.json",
                     SearchOption.AllDirectories))
        {
            string worldFolder = Path.GetDirectoryName(worldDescriptionPath) ?? "";

            if (string.IsNullOrWhiteSpace(worldFolder))
                continue;

            worlds.Add(CreateImportableWorldItem(worldFolder));
        }

        return worlds
            .GroupBy(world => world.FolderPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(world => world.DisplayName)
            .ToList();
    }

    private ImportableWorldItem CreateImportableWorldItem(string worldFolder)
    {
        string folderName = Path.GetFileName(worldFolder);

        string displayName = folderName;

        string jsonPath = Path.Combine(worldFolder, "WorldDescription.json");

        try
        {
            string json = File.ReadAllText(jsonPath);

            using JsonDocument doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("WorldDescription", out JsonElement worldDescription))
            {
                string worldName = "";

                if (worldDescription.TryGetProperty("WorldName", out JsonElement nameElement))
                    worldName = nameElement.GetString() ?? "";

                string islandId = "";

                if (worldDescription.TryGetProperty("islandId", out JsonElement islandElement))
                    islandId = islandElement.GetString() ?? "";

                if (!string.IsNullOrWhiteSpace(worldName))
                    displayName = $"{worldName}  ({folderName})";

                if (!string.IsNullOrWhiteSpace(islandId))
                    displayName += $"  -  {islandId}";
            }
        }
        catch
        {
            displayName = folderName;
        }

        return new ImportableWorldItem
        {
            DisplayName = displayName,
            FolderPath = worldFolder
        };
    }
    private void FindPlayerWorlds_Click(object sender, RoutedEventArgs e)
    {
        using System.Windows.Forms.FolderBrowserDialog dialog = new()
        {
            Description = "Pick a world folder, Worlds folder, RocksDB folder, SaveProfiles folder, or any folder containing Windrose worlds.",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        List<ImportableWorldItem> worlds = FindWorldFoldersInside(dialog.SelectedPath);

        PlayerWorldsListBox.Items.Clear();

        foreach (ImportableWorldItem world in worlds)
        {
            PlayerWorldsListBox.Items.Add(world);
        }

        if (worlds.Count == 0)
        {
            ThemedMessageWindow.Show(
                this,
                "No Worlds Found",
                "No Windrose worlds were found in the selected folder.\n\nPick the world folder itself, the Worlds folder, RocksDB folder, or SaveProfiles folder."
            );
        }
    }
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
    private void CopySelectedWorlds_Click(object sender, RoutedEventArgs e)
    {
        if (PlayerWorldsListBox.SelectedItems.Count == 0)
        {
            WpfMessageBox.Show(
                "Select one or more player worlds first.",
                "No Worlds Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return;
        }

        try
        {
            foreach (object selectedItem in PlayerWorldsListBox.SelectedItems)
            {
                if (selectedItem is ImportableWorldItem importableWorld)
                {
                    WorldImportService.CopyWorldFolderToServer(
                        importableWorld.FolderPath,
                        _serverFolder
                    );

                    continue;
                }

                int selectedIndex = PlayerWorldsListBox.Items.IndexOf(selectedItem);

                if (selectedIndex < 0 || selectedIndex >= _availablePlayerWorlds.Count)
                    continue;

                WorldFolderInfo world = _availablePlayerWorlds[selectedIndex];

                WorldImportService.CopyWorldToServer(world, _serverFolder);
            }

            WorldsChanged = true;

            ThemedMessageWindow.Show(
                this,
                "World Import Complete",
                "Selected world folders copied."
            );
        }
        catch (Exception ex)
        {
            ThemedMessageWindow.Show(
                this,
                "Copy Worlds Failed",
                ex.Message
            );
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = WorldsChanged;
        Close();
    }
}