using System.Diagnostics;
using System.IO;
using System.Windows;
using Elka_windrose_server_control.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace Elka_windrose_server_control.Views;

public partial class WorldBackupsWindow : Window
{
    private readonly string _worldBackupsRoot;
    private readonly string _serverFolder;
    private readonly string _serverName;
    private readonly string _worldId;
    private readonly Func<bool> _isServerRunning;

    private string BackupGroupFolder => Path.Combine(
        _worldBackupsRoot,
        MakeSafeFileName($"{_serverName}_{_worldId}")
    );

    public WorldBackupsWindow(
        string worldBackupsRoot,
        string serverFolder,
        string serverName,
        string worldId,
        Func<bool> isServerRunning)
    {
        InitializeComponent();

        _worldBackupsRoot = worldBackupsRoot;
        _serverFolder = serverFolder;
        _serverName = string.IsNullOrWhiteSpace(serverName) ? "UnnamedServer" : serverName;
        _worldId = worldId;
        _isServerRunning = isServerRunning;

        WorldInfoTextBlock.Text =
            $"Server: {_serverName}\nWorld: {_worldId}\nKeeps latest 5 backups.";

        RefreshBackups();
        UpdateRestoreButtonState();
    }

    private void BackupNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BackupActiveWorld();
            RefreshBackups();

            WpfMessageBox.Show(
                "World backup completed.",
                "Backup Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                ex.Message,
                "Backup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
    private void UpdateRestoreButtonState()
    {
        bool serverRunning = _isServerRunning();

        RestoreSelectedBackupButton.IsEnabled = !serverRunning;

        if (serverRunning)
        {
            RestoreSelectedBackupButton.ToolTip = "Stop the server before restoring a world backup.";
        }
        else
        {
            RestoreSelectedBackupButton.ToolTip = "Restore the selected backup.";
        }
    }
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
    private void BackupsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateRestoreButtonState();
    }
    private void RestoreSelectedBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_isServerRunning())
        {
            WpfMessageBox.Show(
                "Stop the server before restoring a world backup.",
                "Server Is Running",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return;
        }

        if (BackupsListBox.SelectedItem is not BackupListItem selected)
        {
            WpfMessageBox.Show(
                "Select a backup first.",
                "No Backup Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return;
        }

        MessageBoxResult result = WpfMessageBox.Show(
            $"Restore this backup?\n\n{selected.DisplayName}\n\nThis will overwrite the current active world folder.",
            "Restore World Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            string worldsFolder = WorldImportService.GetServerWorldsFolder(_serverFolder);
            string destinationWorldFolder = Path.Combine(worldsFolder, _worldId);

            if (Directory.Exists(destinationWorldFolder))
                Directory.Delete(destinationWorldFolder, recursive: true);

            CopyDirectory(selected.FullPath, destinationWorldFolder);

            WpfMessageBox.Show(
                "World backup restored.",
                "Restore Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                ex.Message,
                "Restore Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
    public static void BackupWorld(
    string worldBackupsRoot,
    string serverFolder,
    string serverName,
    string worldId)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = "UnnamedServer";

        string backupGroupFolder = Path.Combine(
            worldBackupsRoot,
            MakeSafeFileName($"{serverName}_{worldId}")
        );

        string worldsFolder = WorldImportService.GetServerWorldsFolder(serverFolder);
        string sourceWorldFolder = Path.Combine(worldsFolder, worldId);

        if (!Directory.Exists(sourceWorldFolder))
            throw new DirectoryNotFoundException($"Active world folder was not found:\n{sourceWorldFolder}");

        Directory.CreateDirectory(backupGroupFolder);

        string backupFolder = Path.Combine(
            backupGroupFolder,
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
        );

        CopyDirectory(sourceWorldFolder, backupFolder);
        TrimOldBackups(backupGroupFolder, 5);
    }
    private void BackupActiveWorld()
    {
        BackupWorld(
            _worldBackupsRoot,
            _serverFolder,
            _serverName,
            _worldId
        );
    }

    private void RefreshBackups()
    {
        BackupsListBox.Items.Clear();

        if (!Directory.Exists(BackupGroupFolder))
            return;

        foreach (string folder in Directory.GetDirectories(BackupGroupFolder)
                     .OrderByDescending(Directory.GetCreationTimeUtc))
        {
            BackupsListBox.Items.Add(new BackupListItem
            {
                DisplayName = Path.GetFileName(folder),
                FullPath = folder
            });
        }
    }

    private static void TrimOldBackups(string backupGroupFolder, int keepCount)
    {
        List<string> folders = Directory.GetDirectories(backupGroupFolder)
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .ToList();

        foreach (string oldFolder in folders.Skip(keepCount))
        {
            Directory.Delete(oldFolder, recursive: true);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(
                file,
                Path.Combine(destinationDir, Path.GetFileName(file)),
                overwrite: true
            );
        }

        foreach (string directory in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(
                directory,
                Path.Combine(destinationDir, Path.GetFileName(directory))
            );
        }
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed class BackupListItem
    {
        public string DisplayName { get; init; } = "";
        public string FullPath { get; init; } = "";

        public override string ToString() => DisplayName;
    }
}