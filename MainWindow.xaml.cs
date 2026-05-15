using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Net.Http;
using System.IO.Compression;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using WpfMessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;
using Elka_windrose_server_control.Models;
using Elka_windrose_server_control.Services;
using Elka_windrose_server_control.Views;



namespace Elka_windrose_server_control;

public partial class MainWindow : Window
{
    private const string ServerFolderName = "Windrose Dedicated Server";
    private const string StartBatName = "StartServerForeground.bat";
    private const string WindroseDedicatedServerAppId = "4129620";
    private const string SteamCmdZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    private const string DonationUrl = "https://ko-fi.com/msffixit";
    private ServerDescriptionRoot? _serverDescription;
    private List<WorldFolderInfo> _availablePlayerWorlds = [];
    private List<WorldFolderInfo> _importedServerWorlds = [];
    private readonly string _appRoot;
    private readonly string _serverFilesRoot;
    private readonly string _copiedServerFolder;
    private readonly string _backupsRoot;
    private readonly string _logsRoot;
    private readonly string _profilesRoot;
    private readonly string _worldBackupsRoot;
    private Process? _serverProcess;
    private readonly DispatcherTimer _serverStatusTimer = new();
    private readonly DispatcherTimer _serverLogTimer = new();
    private readonly DispatcherTimer _worldBackupTimer = new();
    private string? _currentServerLogFile;
    private long _lastServerLogPosition;
    private readonly HashSet<string> _onlinePlayers = new(StringComparer.OrdinalIgnoreCase);
    private int _sessionJoinCount;
    private int _sessionLeaveCount;
    private const string ServerProcessName = "WindroseServer-Win64-Shipping";
    private const string TailscaleExeName = "tailscale.exe";
    private readonly string _appSettingsPath;
    private bool _manualStopRequested;
    private string? _lockedWorldIslandId;
    private bool _isLoadingWorldSelection;
    private const string GitHubLatestReleaseApi =
    "https://api.github.com/repos/torment78/WindroseServerControl-Releases/releases/latest";

    public MainWindow()
    {
        InitializeComponent();

        _appRoot = AppContext.BaseDirectory;
        _serverFilesRoot = Path.Combine(_appRoot, "ServerFiles");
        _copiedServerFolder = Path.Combine(_serverFilesRoot, ServerFolderName);
        _backupsRoot = Path.Combine(_appRoot, "Backups");
        _logsRoot = Path.Combine(_appRoot, "Logs");
        _profilesRoot = Path.Combine(_appRoot, "Profiles");
        _worldBackupsRoot = Path.Combine(_appRoot, "WorldBackups");
        _appSettingsPath = Path.Combine(_appRoot, "appsettings.json");  

        EnsureAppFolders();

        CopiedServerPathTextBox.Text = _copiedServerFolder;

        Log("Application started.");
        Log($"App root: {_appRoot}");

        LoadAppSettings();

        TryAutoLoadServerDescription();
        RefreshProfilesList();

        Loaded += MainWindow_Loaded;

        SetupServerStatusTimer();

        SetupServerLogTimer();
        SetupWorldBackupTimer();
        UpdateServerStatus();
        RebuildLivePlayersFromNewestLog();
    }

    private void EnsureAppFolders()
    {
        Directory.CreateDirectory(_serverFilesRoot);
        Directory.CreateDirectory(_backupsRoot);
        Directory.CreateDirectory(_logsRoot);
        Directory.CreateDirectory(_profilesRoot);
        Directory.CreateDirectory(_worldBackupsRoot);
    }
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        DragMove();
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreWindow_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
    private void CopyStandardServer_Click(object sender, RoutedEventArgs e)
    {
        string standardPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            ServerFolderName
        );

        if (!Directory.Exists(standardPath))
        {
            Log($"Standard Steam folder not found: {standardPath}");
            WpfMessageBox.Show(
                "Standard Steam folder was not found. Use custom folder path instead.",
                "Folder Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        CopyServerFolder(standardPath);
    }

    private void BrowseAndCopyServer_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select the Windrose Dedicated Server folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
            return;

        string selectedPath = dialog.SelectedPath;
        string selectedFolderName = Path.GetFileName(selectedPath.TrimEnd('\\', '/'));

        if (!selectedFolderName.Equals(ServerFolderName, StringComparison.OrdinalIgnoreCase))
        {
            WpfMessageBox.Show(
                $"Please select the folder named:\n\n{ServerFolderName}",
                "Wrong Folder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            Log($"Wrong folder selected: {selectedPath}");
            return;
        }

        CopyServerFolder(selectedPath);
    }

    private void CopyServerFolder(string sourceFolder)
    {
        try
        {
            Log($"Copy started from: {sourceFolder}");

            if (Directory.Exists(_copiedServerFolder))
            {
                BackupWorldsAndServerDescription();

                Directory.Delete(_copiedServerFolder, recursive: true);

                Log("Existing copied server deleted after backing up Worlds and ServerDescription.json.");
            }

            CopyDirectory(sourceFolder, _copiedServerFolder);

            CopiedServerPathTextBox.Text = _copiedServerFolder;

            Log("Server folder copied successfully.");
            Log($"Copied to: {_copiedServerFolder}");

            WpfMessageBox.Show(
                "Windrose Dedicated Server folder copied successfully.",
                "Copy Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            Log($"ERROR copying server folder: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Copy Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string destinationFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (string directory in Directory.GetDirectories(sourceDir))
        {
            string directoryName = Path.GetFileName(directory);
            string destinationSubDir = Path.Combine(destinationDir, directoryName);
            CopyDirectory(directory, destinationSubDir);
        }
    }
    private void SetupServerStatusTimer()
    {
        _serverStatusTimer.Interval = TimeSpan.FromSeconds(1);
        _serverStatusTimer.Tick += ServerStatusTimer_Tick;
        _serverStatusTimer.Start();

        Log("Server status monitor started.");
    }

    private bool IsServerRunning()
    {
        return Process.GetProcessesByName(ServerProcessName).Any();
    }

    private void UpdateServerStatus()
    {
        bool isRunning = IsServerRunning();

        if (isRunning)
        {
            ServerStatusTextBlock.Text = "Running";
            StartServerButton.IsEnabled = false;
            StopServerButton.IsEnabled = true;
        }
        else
        {
            ServerStatusTextBlock.Text = "Stopped";
            StartServerButton.IsEnabled = true;
            StopServerButton.IsEnabled = false;
        }
        UpdateServerTickRate();
    }
    private bool ValidateWorldBeforeStart()
    {
        string selectedWorldId = WorldIslandIdTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(selectedWorldId))
        {
            WpfMessageBox.Show(
                "No World Island ID is selected.\n\nSelect an imported world before starting the server.",
                "Missing World",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return false;
        }

        bool worldExists = _importedServerWorlds.Any(world =>
            world.WorldId.Equals(selectedWorldId, StringComparison.OrdinalIgnoreCase));

        if (!worldExists)
        {
            WpfMessageBox.Show(
                $"The selected world does not exist in the imported server worlds folder:\n\n{selectedWorldId}\n\nImport the world again or select another world.",
                "World Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return false;
        }

        return true;
    }
    private void StartServer_Click(object sender, RoutedEventArgs e)
    {
        string batPath = Path.Combine(GetCurrentServerFolder(), StartBatName);

        try
        {
            RefreshImportedServerWorlds();
            SelectCurrentWorldFromJson();
            ApplyLockedWorldIfAvailable();

            if (!ValidateWorldBeforeStart())
                return;

            SaveServerDescription_Click(sender, e);

            _manualStopRequested = false;

            bool hiddenMode = HiddenWindowRadioButton.IsChecked == true;

            if (hiddenMode)
            {
                string exePath = Path.Combine(
                    GetCurrentServerFolder(),
                    "R5",
                    "Binaries",
                    "Win64",
                    "WindroseServer-Win64-Shipping.exe"
                );

                if (!File.Exists(exePath))
                {
                    Log($"Server EXE not found: {exePath}");

                    WpfMessageBox.Show(
                        $"Could not find:\n\n{exePath}",
                        "Server EXE Missing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }

                _serverProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-log",
                    WorkingDirectory = GetCurrentServerFolder(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                Log($"Started hidden server EXE: {exePath}");
            }
            else
            {
                if (!File.Exists(batPath))
                {
                    Log($"Start BAT not found: {batPath}");

                    WpfMessageBox.Show(
                        $"Could not find:\n\n{batPath}",
                        "Start File Missing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }

                _serverProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    WorkingDirectory = Path.GetDirectoryName(batPath),
                    UseShellExecute = true
                });

                Log($"Started visible server BAT: {batPath}");
            }

            UpdateServerStatus();
        }
        catch (Exception ex)
        {
            Log($"ERROR starting server: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Start Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _manualStopRequested = true;
            bool stoppedAny = false;

            // First try the tracked process.
            // In hidden mode this is the real server EXE.
            // In visible mode this may only be the BAT wrapper.
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                Log($"Trying to stop tracked process: {_serverProcess.ProcessName} PID {_serverProcess.Id}");

                _serverProcess.CloseMainWindow();

                if (!_serverProcess.WaitForExit(5000))
                {
                    _serverProcess.Kill(entireProcessTree: true);
                    Log("Killed tracked process tree.");
                }
                else
                {
                    Log("Tracked process closed safely.");
                }

                stoppedAny = true;
            }

            // Then try the real Windrose server process name.
            // This is needed for visible BAT mode because the BAT launches the EXE separately.
            string[] possibleProcessNames =
            [
                "WindroseServer-Win64-Shipping"
            ];

            foreach (string processName in possibleProcessNames)
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        Log($"Trying to stop process: {process.ProcessName} PID {process.Id}");

                        process.CloseMainWindow();

                        if (!process.WaitForExit(5000))
                        {
                            process.Kill(entireProcessTree: true);
                            Log($"Killed process tree: {process.ProcessName} PID {process.Id}");
                        }
                        else
                        {
                            Log($"Closed process safely: {process.ProcessName} PID {process.Id}");
                        }

                        stoppedAny = true;
                    }
                    catch (Exception innerEx)
                    {
                        Log($"Could not stop {process.ProcessName}: {innerEx.Message}");
                    }
                }
            }

            if (!stoppedAny)
            {
                Log("No matching Windrose server process was found.");
                WpfMessageBox.Show(
                    "No running Windrose server process was found.",
                    "Server Not Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            UpdateServerStatus();
        }
        catch (Exception ex)
        {
            Log($"ERROR stopping server: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Stop Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
    private string GetCurrentServerFolder()
    {
        // Future multi-server support will switch this to:
        // return _currentServerInstanceFolder;
        return _copiedServerFolder;
    }
    private string GetSteamCmdFolder()
    {
        return Path.Combine(_appRoot, "Tools", "SteamCMD");
    }

    private string GetSteamCmdExePath()
    {
        return Path.Combine(GetSteamCmdFolder(), "steamcmd.exe");
    }
    private string GetServerDescriptionJsonPath()
    {
        return Path.Combine(
            GetCurrentServerFolder(),
            "R5",
            "ServerDescription.json"
        );
    }

    private void ApplyLockedWorldIfAvailable()
    {
        if (string.IsNullOrWhiteSpace(_lockedWorldIslandId))
            return;

        RefreshImportedServerWorlds();

        bool lockedWorldExists = _importedServerWorlds.Any(world =>
            world.WorldId.Equals(_lockedWorldIslandId, StringComparison.OrdinalIgnoreCase));

        if (!lockedWorldExists)
        {
            Log($"Locked world not found in imported server worlds: {_lockedWorldIslandId}");
            return;
        }

        WorldIslandIdTextBox.Text = _lockedWorldIslandId;

        if (_serverDescription?.ServerDescription_Persistent != null)
        {
            _serverDescription.ServerDescription_Persistent.WorldIslandId = _lockedWorldIslandId;

            Log($"Applied locked world to active config: {_lockedWorldIslandId}");
        }

        for (int i = 0; i < _importedServerWorlds.Count; i++)
        {
            if (_importedServerWorlds[i].WorldId.Equals(_lockedWorldIslandId, StringComparison.OrdinalIgnoreCase))
            {
                ServerWorldsComboBox.SelectedIndex = i;
                break;
            }
        }
    }
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RunFirstRunSetupIfNeeded();
    }
    private void ShowDonationPopup()
    {
        AppSettings settings = AppSettingsService.Load(_appSettingsPath);

        if (settings.DonationPopupDisabled)
            return;

        DonationWindow window = new(DonationUrl)
        {
            Owner = this
        };

        bool? result = window.ShowDialog();

        if (result == true && window.DisableFuturePopups)
        {
            settings.DonationPopupDisabled = true;
            AppSettingsService.Save(_appSettingsPath, settings);

            Log("Donation popup disabled by user.");
        }
    }
    private async Task CheckAppUpdateAsync()
    {
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WindroseServerControl-Updater");

            string json = await client.GetStringAsync(GitHubLatestReleaseApi);

            using JsonDocument doc = JsonDocument.Parse(json);

            string latestTag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            string latestVersionText = latestTag.TrimStart('v', 'V');

            Version currentVersion =
                Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

            if (!Version.TryParse(latestVersionText, out Version? latestVersion))
            {
                Log($"Could not parse latest app version: {latestTag}");
                return;
            }

            if (latestVersion <= currentVersion)
            {
                Log($"App is up to date. Current: {currentVersion}, Latest: {latestVersion}");
                return;
            }
            AppSettings settings = AppSettingsService.Load(_appSettingsPath);

            if (settings.SkippedAppVersion == latestVersion.ToString())
            {
                Log($"App update skipped by user: {latestVersion}");
                return;
            }

            string? downloadUrl = null;

            foreach (JsonElement asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                Log("App update found, but no installer EXE asset was found.");
                return;
            }

            string releaseNotes = "";

            if (doc.RootElement.TryGetProperty("body", out JsonElement bodyElement))
            {
                releaseNotes = bodyElement.GetString() ?? "";
            }

            AppUpdateWindow updateWindow = new(
                currentVersion.ToString(),
                latestVersion.ToString(),
                releaseNotes
            )
            {
                Owner = this
            };

            bool? result = updateWindow.ShowDialog();
            if (result == true && updateWindow.ShouldSkipVersion)
            {
                settings.SkippedAppVersion = latestVersion.ToString();
                AppSettingsService.Save(_appSettingsPath, settings);

                Log($"User skipped app version: {latestVersion}");
                return;
            }

            if (result != true || !updateWindow.ShouldInstall)
                return;

            await DownloadAndLaunchAppUpdateAsync(downloadUrl, latestVersion.ToString(), updateWindow);

            if (result != true || !updateWindow.ShouldInstall)
                return;

            await DownloadAndLaunchAppUpdateAsync(
                 downloadUrl,
                 latestVersion.ToString(),
                 updateWindow
            );
        }
        catch (Exception ex)
        {
            Log($"App update check failed: {ex.Message}");
        }
    }
    private string GetCurrentExePath()
    {
        return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
    }
    private async Task DownloadAndLaunchAppUpdateAsync(string downloadUrl, string version, AppUpdateWindow updateWindow)
    {
        string updatesFolder = Path.Combine(_appRoot, "Updates");
        Directory.CreateDirectory(updatesFolder);

        string installerPath = Path.Combine(
            updatesFolder,
            $"WindroseServerControl_Setup_v{version}.exe"
        );

        Log($"Downloading app update: {downloadUrl}");

        using HttpClient client = new();

        using HttpResponseMessage response = await client.GetAsync(
            downloadUrl,
            HttpCompletionOption.ResponseHeadersRead
        );

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        await using Stream input = await response.Content.ReadAsStreamAsync();
        await using FileStream output = File.Create(installerPath);

        byte[] buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            int read = await input.ReadAsync(buffer);

            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read));

            totalRead += read;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                double percent = totalRead * 100.0 / totalBytes.Value;

                updateWindow.SetDownloadProgress(
                    percent,
                    $"Downloading update... {percent:0}%"
                );
            }
            else
            {
                updateWindow.SetDownloadProgress(
                    0,
                    $"Downloading update... {totalRead / 1024 / 1024} MB"
                );
            }
        }

        updateWindow.SetDownloadProgress(100, "Download complete. Starting installer...");

        Log($"Downloaded update installer: {installerPath}");

        string currentExePath = GetCurrentExePath();

        string installDir = Path.GetDirectoryName(currentExePath) ?? _appRoot;

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments =
                "-NoProfile -ExecutionPolicy Bypass -Command " +
                $"\"Start-Process -FilePath '{installerPath}' -ArgumentList '/VERYSILENT /NORESTART /SP- /SUPPRESSMSGBOXES /DIR=\\\"{installDir}\\\"' -Wait; " +
                $"Start-Process -FilePath '{currentExePath}'\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        Close();
    }
    private bool NeedsFirstRunSetup()
    {
        bool steamCmdMissing = !File.Exists(GetSteamCmdExePath());

        bool serverExeMissing = !File.Exists(Path.Combine(
            GetCurrentServerFolder(),
            "R5",
            "Binaries",
            "Win64",
            "WindroseServer-Win64-Shipping.exe"
        ));

        bool serverJsonMissing = !File.Exists(GetServerDescriptionJsonPath());

        return steamCmdMissing || serverExeMissing || serverJsonMissing;
    }
    private async void RunFirstRunSetupIfNeeded()
    {
        if (!NeedsFirstRunSetup())
        {
            Log("First-run setup skipped. SteamCMD, server EXE, and ServerDescription.json already exist.");

            ShowDonationPopup();

            _ = CheckSteamServerUpdateStatusOnlyAsync();
            _ = CheckAppUpdateAsync();

            return;
        }

        MessageBoxResult result = WpfMessageBox.Show(
            "This PC is missing one or more required server files.\n\nIs this the PC that will run the Windrose dedicated server?\n\nYes = install/update server here\nNo = open world import only, then close the app",
            "Windrose Server Setup",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Cancel)
            return;

        if (result == MessageBoxResult.No)
        {
            OpenWorldImport_Click(this, new RoutedEventArgs());
            Close();
            return;
        }

        ThemedMessageWindow.Show(
           this,
           "Setting Up Server",
           "Setting up the Windrose dedicated server now.\n\nThe app will download SteamCMD if needed, install or update the server, generate the default config, and then load the server settings.\n\nDepending on your internet speed, this may take a few moments.\n\nPlease wait until you see the setup complete message before changing settings or starting the server."
        );

        await InstallOrUpdateServerWithSteamCmdAsync();

        ThemedMessageWindow.Show(
                            this,
           "Setup Complete",
           "Server setup is complete.\n\nYou can now configure your server settings."
        );
    }
    private void TryAutoLoadServerDescription()
    {
        string jsonPath = GetServerDescriptionJsonPath();

        if (!File.Exists(jsonPath))
        {
            Log($"Auto-load skipped. ServerDescription.json not found: {jsonPath}");
            return;
        }

        try
        {
            _serverDescription = ServerDescriptionService.Load(jsonPath);

            var settings = _serverDescription.ServerDescription_Persistent!;

            DeploymentIdTextBox.Text = _serverDescription.DeploymentId ?? "";
            PersistentServerIdTextBox.Text = settings.PersistentServerId ?? "";
            InviteCodeTextBox.Text = settings.InviteCode ?? "";
            IsPasswordProtectedCheckBox.IsChecked = settings.IsPasswordProtected;
            PasswordTextBox.Text = settings.Password ?? "";
            ServerNameTextBox.Text = settings.ServerName ?? "";
            WorldIslandIdTextBox.Text = settings.WorldIslandId ?? "";
            MaxPlayerCountTextBox.Text = settings.MaxPlayerCount.ToString();
            UserSelectedRegionTextBox.Text = settings.UserSelectedRegion ?? "";
            P2pProxyAddressTextBox.Text = settings.P2pProxyAddress ?? "";
            UseDirectConnectionCheckBox.IsChecked = settings.UseDirectConnection;
            DirectConnectionServerAddressTextBox.Text = settings.DirectConnectionServerAddress ?? "";
            DirectConnectionServerPortTextBox.Text = settings.DirectConnectionServerPort.ToString();
            DirectConnectionProxyAddressTextBox.Text = settings.DirectConnectionProxyAddress ?? "";
            AutoLoadLatestBackupIfHasBrokenCheckBox.IsChecked = settings.AutoLoadLatestBackupIfHasBroken;

            RefreshImportedServerWorlds();
            SelectCurrentWorldFromJson();
            ApplyLockedWorldIfAvailable();
            AppSettings appSettings = AppSettingsService.Load(_appSettingsPath);
            Log($"Loaded LastActiveWorldPreset from app settings: {appSettings.LastActiveWorldPreset ?? "NULL"}");
            Log($"App settings path: {_appSettingsPath}");
            ActiveWorldPresetTextBlock.Text = string.IsNullOrWhiteSpace(appSettings.LastActiveWorldPreset)
                ? "World preset: Default / Not loaded"
                : $"World preset: {appSettings.LastActiveWorldPreset}";
            
           
            RefreshServerSummary();

            Log($"Auto-loaded ServerDescription.json: {jsonPath}");
        }
        catch (Exception ex)
        {
            Log($"Auto-load failed: {ex.Message}");
        }
    }
    private void LoadServerDescription_Click(object sender, RoutedEventArgs e)
    {
        string jsonPath = GetServerDescriptionJsonPath();

        try
        {
            _serverDescription = ServerDescriptionService.Load(jsonPath);

            var settings = _serverDescription.ServerDescription_Persistent!;

            DeploymentIdTextBox.Text = _serverDescription.DeploymentId ?? "";
            PersistentServerIdTextBox.Text = settings.PersistentServerId ?? "";
            InviteCodeTextBox.Text = settings.InviteCode ?? "";
            IsPasswordProtectedCheckBox.IsChecked = settings.IsPasswordProtected;
            PasswordTextBox.Text = settings.Password ?? "";
            ServerNameTextBox.Text = settings.ServerName ?? "";
            WorldIslandIdTextBox.Text = settings.WorldIslandId ?? "";
            MaxPlayerCountTextBox.Text = settings.MaxPlayerCount.ToString();
            UserSelectedRegionTextBox.Text = settings.UserSelectedRegion ?? "";
            P2pProxyAddressTextBox.Text = settings.P2pProxyAddress ?? "";
            UseDirectConnectionCheckBox.IsChecked = settings.UseDirectConnection;
            DirectConnectionServerAddressTextBox.Text = settings.DirectConnectionServerAddress ?? "";
            DirectConnectionServerPortTextBox.Text = settings.DirectConnectionServerPort.ToString();
            DirectConnectionProxyAddressTextBox.Text = settings.DirectConnectionProxyAddress ?? "";
            AutoLoadLatestBackupIfHasBrokenCheckBox.IsChecked = settings.AutoLoadLatestBackupIfHasBroken;

            RefreshImportedServerWorlds();
            SelectCurrentWorldFromJson();
            ApplyLockedWorldIfAvailable();
            RefreshServerSummary();

            Log($"Loaded server description JSON: {jsonPath}");
        }
        catch (Exception ex)
        {
            Log($"ERROR loading server description JSON: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Load Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void OpenServerSettings_Click(object sender, RoutedEventArgs e)
    {
        ServerSettingsWindow window = new()
        {
            Owner = this
        };

        window.ServerNameTextBox.Text = ServerNameTextBox.Text;
        window.InviteCodeTextBox.Text = InviteCodeTextBox.Text;
        window.MaxPlayerCountTextBox.Text = MaxPlayerCountTextBox.Text;
        window.RegionTextBox.Text = UserSelectedRegionTextBox.Text;
        window.PasswordProtectedCheckBox.IsChecked = IsPasswordProtectedCheckBox.IsChecked;
        window.PasswordTextBox.Text = PasswordTextBox.Text;
        window.AutoLoadLatestBackupIfHasBrokenCheckBox.IsChecked = AutoLoadLatestBackupIfHasBrokenCheckBox.IsChecked;
        window.P2pProxyAddressTextBox.Text = P2pProxyAddressTextBox.Text;
        window.UseDirectConnectionCheckBox.IsChecked = UseDirectConnectionCheckBox.IsChecked;
        window.DirectConnectionAddressTextBox.Text = DirectConnectionServerAddressTextBox.Text;
        window.DirectConnectionPortTextBox.Text = DirectConnectionServerPortTextBox.Text;
        window.ProxyAddressTextBox.Text = DirectConnectionProxyAddressTextBox.Text;

        bool? result = window.ShowDialog();

        if (result != true)
            return;

        ServerNameTextBox.Text = window.ServerNameTextBox.Text;
        InviteCodeTextBox.Text = window.InviteCodeTextBox.Text;
        MaxPlayerCountTextBox.Text = window.MaxPlayerCountTextBox.Text;
        UserSelectedRegionTextBox.Text = window.RegionTextBox.Text;
        IsPasswordProtectedCheckBox.IsChecked = window.PasswordProtectedCheckBox.IsChecked;
        PasswordTextBox.Text = window.PasswordTextBox.Text;
        AutoLoadLatestBackupIfHasBrokenCheckBox.IsChecked = window.AutoLoadLatestBackupIfHasBrokenCheckBox.IsChecked;
        P2pProxyAddressTextBox.Text = window.P2pProxyAddressTextBox.Text;

        UseDirectConnectionCheckBox.IsChecked = window.UseDirectConnectionCheckBox.IsChecked;
        DirectConnectionServerAddressTextBox.Text = window.DirectConnectionAddressTextBox.Text;
        DirectConnectionServerPortTextBox.Text = window.DirectConnectionPortTextBox.Text;
        DirectConnectionProxyAddressTextBox.Text = window.ProxyAddressTextBox.Text;
        SaveServerDescription_Click(sender, e);
        RefreshServerSummary();
        Log("Server settings updated from modal window.");
    }
    private void RefreshServerSummary()
    {
        ServerSummaryTextBlock.Text =
            $"Server name: {ServerNameTextBox.Text}\n" +
            $"Invite code: {InviteCodeTextBox.Text}\n" +
            $"Password protected: {IsPasswordProtectedCheckBox.IsChecked == true}\n" +
            $"Max players: {MaxPlayerCountTextBox.Text}\n" +
            $"Region: {UserSelectedRegionTextBox.Text}\n" +
            $"World: {WorldIslandIdTextBox.Text}\n" +
            $"Direct connect: {UseDirectConnectionCheckBox.IsChecked == true}\n" +
            $"Direct connect server address: {DirectConnectionServerAddressTextBox.Text}\n" +
            $"Direct connect server port: {DirectConnectionServerPortTextBox.Text}\n" +
            $"Direct connection proxy address: {DirectConnectionProxyAddressTextBox.Text}";
    }

    private void SaveServerDescription_Click(object sender, RoutedEventArgs e)
    {
        string jsonPath = GetServerDescriptionJsonPath();

        try
        {
            _serverDescription ??= ServerDescriptionService.Load(jsonPath);

            var settings = _serverDescription.ServerDescription_Persistent!;

            

            settings.InviteCode = InviteCodeTextBox.Text.Trim();
            settings.IsPasswordProtected = IsPasswordProtectedCheckBox.IsChecked == true;
            settings.Password = PasswordTextBox.Text.Trim();
            settings.ServerName = ServerNameTextBox.Text.Trim();
            settings.WorldIslandId = WorldIslandIdTextBox.Text.Trim();
            settings.UserSelectedRegion = UserSelectedRegionTextBox.Text.Trim();
            settings.P2pProxyAddress = P2pProxyAddressTextBox.Text.Trim();
            settings.UseDirectConnection = UseDirectConnectionCheckBox.IsChecked == true;
            settings.DirectConnectionServerAddress = DirectConnectionServerAddressTextBox.Text.Trim();
            settings.DirectConnectionProxyAddress = DirectConnectionProxyAddressTextBox.Text.Trim();
            settings.AutoLoadLatestBackupIfHasBroken = AutoLoadLatestBackupIfHasBrokenCheckBox.IsChecked == true;

            if (int.TryParse(MaxPlayerCountTextBox.Text.Trim(), out int maxPlayers))
                settings.MaxPlayerCount = maxPlayers;

            if (int.TryParse(DirectConnectionServerPortTextBox.Text.Trim(), out int directPort))
                settings.DirectConnectionServerPort = directPort;

            ServerDescriptionService.Backup(jsonPath, _backupsRoot);
            ServerDescriptionService.Save(jsonPath, _serverDescription);

            Log("Backed up old server description JSON.");
            Log($"Saved server description JSON: {jsonPath}");

            WpfMessageBox.Show(
                "Server description saved.",
                "Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            Log($"ERROR saving server description JSON: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void OpenFolders_Click(object sender, RoutedEventArgs e)
    {
        FoldersWindow window = new(
            GetCurrentServerFolder(),
            _backupsRoot,
            _logsRoot,
            GetServerLogsFolder())
        {
            Owner = this
        };

        window.ShowDialog();

        Log("Opened folders modal.");
    }
    private void OpenWorldImport_Click(object sender, RoutedEventArgs e)
    {
        WorldImportWindow window = new(GetCurrentServerFolder())
        {
            Owner = this
        };

        bool? result = window.ShowDialog();

        if (result == true)
        {
            RefreshImportedServerWorlds();
            SelectCurrentWorldFromJson();
            ApplyLockedWorldIfAvailable();
            RefreshServerSummary();

            Log("World import completed from modal window.");
        }
    }

    

   

    private void RefreshImportedServerWorlds()
    {
        _importedServerWorlds = WorldImportService.FindImportedServerWorlds(GetCurrentServerFolder());
        ServerWorldsComboBox.Items.Clear();

        foreach (WorldFolderInfo world in _importedServerWorlds)
        {
            ServerWorldsComboBox.Items.Add($"{world.WorldId}   |   Created: {world.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        }

        Log($"Imported server worlds found: {_importedServerWorlds.Count}");
        Log($"Server worlds folder: {WorldImportService.GetServerWorldsFolder(GetCurrentServerFolder())}");
    }
    private void SelectCurrentWorldFromJson()
    {
        if (_serverDescription?.ServerDescription_Persistent == null)
            return;

        string? currentWorldId = _serverDescription.ServerDescription_Persistent.WorldIslandId;

        if (string.IsNullOrWhiteSpace(currentWorldId))
            return;

        for (int i = 0; i < _importedServerWorlds.Count; i++)
        {
            if (_importedServerWorlds[i].WorldId.Equals(
                    currentWorldId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _isLoadingWorldSelection = true;
                ServerWorldsComboBox.SelectedIndex = i;
                _isLoadingWorldSelection = false;

                WorldIslandIdTextBox.Text = currentWorldId;

                Log($"Displayed current world from JSON: {currentWorldId}");
                return;
            }
        }

        Log($"Current JSON world was not found in imported server worlds: {currentWorldId}");
    }
    private void OpenWorldBackups_Click(object sender, RoutedEventArgs e)
    {
        string worldId = WorldIslandIdTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(worldId))
        {
            WpfMessageBox.Show(
                "Select an active world first.",
                "Missing World",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return;
        }

        WorldBackupsWindow window = new(
            _worldBackupsRoot,
            GetCurrentServerFolder(),
            ServerNameTextBox.Text.Trim(),
            worldId,
            IsServerRunning)
        {
            Owner = this
        };

        window.ShowDialog();

        Log("Opened world backups window.");
    }


    private void ApplySelectedWorld_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string selectedWorldId = WorldIslandIdTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(selectedWorldId))
            {
                WpfMessageBox.Show(
                    "No selected world to apply.",
                    "Missing World",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            _lockedWorldIslandId = selectedWorldId;
            SaveAppSettings();

            if (_serverDescription == null)
                _serverDescription = ServerDescriptionService.Load(GetServerDescriptionJsonPath());

            _serverDescription.ServerDescription_Persistent!.WorldIslandId = selectedWorldId;

            ServerDescriptionService.Backup(GetServerDescriptionJsonPath(), _backupsRoot);
            ServerDescriptionService.Save(GetServerDescriptionJsonPath(), _serverDescription);

            Log($"Re-applied and locked selected world: {selectedWorldId}");

            WpfMessageBox.Show(
                $"Selected world applied and saved:\n\n{selectedWorldId}",
                "World Applied",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            Log($"ERROR repairing selected world: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Repair Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
    private void RepairSelectedWorld_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string selectedWorldId = WorldIslandIdTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(selectedWorldId))
            {
                WpfMessageBox.Show(
                    "No selected world to apply.",
                    "Missing World",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            _lockedWorldIslandId = selectedWorldId;
            SaveAppSettings();

            _serverDescription ??= ServerDescriptionService.Load(GetServerDescriptionJsonPath());
            _serverDescription.ServerDescription_Persistent!.WorldIslandId = selectedWorldId;

            ServerDescriptionService.Backup(GetServerDescriptionJsonPath(), _backupsRoot);
            ServerDescriptionService.Save(GetServerDescriptionJsonPath(), _serverDescription);

            RefreshServerSummary();

            Log($"Applied and saved selected world: {selectedWorldId}");

            WpfMessageBox.Show(
                $"Selected world applied and saved:\n\n{selectedWorldId}",
                "World Applied",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            Log($"ERROR applying selected world: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Apply World Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
    private void ServerWorldsComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        int index = ServerWorldsComboBox.SelectedIndex;

        if (index < 0 || index >= _importedServerWorlds.Count)
            return;

        string worldId = _importedServerWorlds[index].WorldId;

        WorldIslandIdTextBox.Text = worldId;

        if (_isLoadingWorldSelection)
        {
            Log($"World displayed without changing lock: {worldId}");
            return;
        }

        _lockedWorldIslandId = worldId;
        SaveAppSettings();
        RefreshServerSummary();

        Log($"User selected and locked active world: {worldId}");
    }
    private void RunHealthCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            List<string> results = [];

            string serverFolder = GetCurrentServerFolder();
            string batPath = Path.Combine(serverFolder, StartBatName);
            string jsonPath = GetServerDescriptionJsonPath();
            string exePath = Path.Combine(
                serverFolder,
                "R5",
                "Binaries",
                "Win64",
                "WindroseServer-Win64-Shipping.exe"
            );

            string worldsFolder = WorldImportService.GetServerWorldsFolder(serverFolder);
            string selectedWorldId = WorldIslandIdTextBox.Text.Trim();
            string selectedWorldPath = Path.Combine(worldsFolder, selectedWorldId);

            AddHealthResult(results, Directory.Exists(serverFolder), "Server folder", serverFolder);
            AddHealthResult(results, File.Exists(batPath), "Start BAT", batPath);
            AddHealthResult(results, File.Exists(jsonPath), "ServerDescription.json", jsonPath);
            AddHealthResult(results, File.Exists(exePath), "Server EXE", exePath);
            AddHealthResult(results, Directory.Exists(worldsFolder), "Server worlds folder", worldsFolder);

            if (string.IsNullOrWhiteSpace(selectedWorldId))
            {
                results.Add("✖ Selected WorldIslandId: missing");
            }
            else
            {
                AddHealthResult(results, Directory.Exists(selectedWorldPath), "Selected world folder", selectedWorldPath);
            }

            string report = string.Join(Environment.NewLine, results);

            Log("Health check completed.");
            Log(report);

            WpfMessageBox.Show(
                report,
                "Server Health Check",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            Log($"ERROR running health check: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Health Check Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private static void AddHealthResult(List<string> results, bool ok, string label, string path)
    {
        string marker = ok ? "✔" : "✖";
        results.Add($"{marker} {label}: {path}");
    }


    private void ServerStatusTimer_Tick(object? sender, EventArgs e)
    {
        bool wasTrackedAndExited =
            _serverProcess != null &&
            _serverProcess.HasExited;

        UpdateServerStatus();
        UpdateServerTickRate();

        if (wasTrackedAndExited &&
            !_manualStopRequested &&
            AutoRestartCheckBox.IsChecked == true)
        {
            Log("Server process exited unexpectedly. Auto restart is enabled.");

            StartServer_Click(this, new RoutedEventArgs());
        }
    }
    private void UpdateServerTickRate()
    {
        // Placeholder for now.
        // Later we can read this from server log output if Windrose prints tick/FPS info.
        ServerTickRateTextBlock.Text = IsServerRunning()
            ? "Running - tick rate not detected yet"
            : "Stopped";
    }
    private string? GetSelectedServerWorldFolder()
    {
        string worldsRoot = WorldImportService.GetServerWorldsFolder(GetCurrentServerFolder());

        if (!Directory.Exists(worldsRoot))
            return null;

        string selectedText = ServerWorldsComboBox.SelectedItem?.ToString() ?? "";

        // 1. Try direct selected folder name
        if (!string.IsNullOrWhiteSpace(selectedText))
        {
            string directPath = Path.Combine(worldsRoot, selectedText);

            if (Directory.Exists(directPath))
                return directPath;
        }

        // 2. Try current WorldIslandId from textbox
        string currentWorldId = WorldIslandIdTextBox.Text.Trim();

        if (!string.IsNullOrWhiteSpace(currentWorldId))
        {
            foreach (string folder in Directory.GetDirectories(worldsRoot))
            {
                string folderName = Path.GetFileName(folder);

                if (folderName.Equals(currentWorldId, StringComparison.OrdinalIgnoreCase))
                    return folder;
            }
        }

        // 3. If only one world exists, use it
        string[] worldFolders = Directory.GetDirectories(worldsRoot);

        if (worldFolders.Length == 1)
            return worldFolders[0];

        return null;
    }
    private void ApplySavedServerSettingsForProfile(string profileName)
    {
        try
        {
            string? selectedWorldFolder = GetSelectedServerWorldFolder();

            if (string.IsNullOrWhiteSpace(selectedWorldFolder))
                return;

            string profileFolder = WorldDescriptionService.GetProfileFolder(selectedWorldFolder, profileName);
            string savedServerJsonPath = Path.Combine(profileFolder, "ServerDescription.json");

            if (!File.Exists(savedServerJsonPath))
                return;

            bool result = ThemedConfirmWindow.Show(
             this,
              "Apply Profile Server Settings",
              $"This profile includes saved server settings.\n\nApply server settings from profile '{profileName}'?"
            );

            if (!result)
                return;

            string activeJson = GetServerDescriptionJsonPath();

            ServerDescriptionService.Backup(activeJson, _backupsRoot);

            File.Copy(
                savedServerJsonPath,
                activeJson,
                overwrite: true
            );

            Log($"Applied saved server settings for profile: {profileName}");
        }
        catch (Exception ex)
        {
            Log($"ERROR applying saved server settings for profile '{profileName}': {ex.Message}");
        }
    }
    private void ApplyWorldPresetForProfile(string profileName)
    {
        try
        {
            string? selectedWorldFolder = GetSelectedServerWorldFolder();

            if (string.IsNullOrWhiteSpace(selectedWorldFolder))
            {
                Log($"World preset skipped. No selected world folder for profile: {profileName}");
                return;
            }

            string presetPath = WorldDescriptionService.GetProfileWorldDescriptionPath(
                selectedWorldFolder,
                profileName
            );

            if (!File.Exists(presetPath))
            {
                Log($"No world preset found for profile: {profileName}");
                return;
            }

            string activeWorldDescriptionPath = WorldDescriptionService.GetWorldDescriptionPath(
                selectedWorldFolder
            );

            File.Copy(
                presetPath,
                activeWorldDescriptionPath,
                overwrite: true
            );

            Log($"Applied world preset for profile: {profileName}");
            Log($"WorldDescription.json updated: {activeWorldDescriptionPath}");
            ActiveWorldPresetTextBlock.Text = $"World preset: {profileName}";
            AppSettings presetSettings = AppSettingsService.Load(_appSettingsPath);
            presetSettings.LastActiveWorldPreset = profileName;
            AppSettingsService.Save(_appSettingsPath, presetSettings);
            AppSettings testSettings = AppSettingsService.Load(_appSettingsPath);
            Log($"TEST saved preset now reads as: {testSettings.LastActiveWorldPreset ?? "NULL"}");
            Log($"Saved LastActiveWorldPreset to app settings: {profileName}");
            Log($"App settings path: {_appSettingsPath}");
        }
        catch (Exception ex)
        {
            Log($"ERROR applying world preset for profile '{profileName}': {ex.Message}");

            ThemedMessageWindow.Show(
                this,
                "World Preset Failed",
                $"The server profile loaded, but the world preset could not be applied.\n\n{ex.Message}"
            );
        }
    }
    private void OpenProfiles_Click(object sender, RoutedEventArgs e)
    {
        ProfilesWindow window = new(_profilesRoot, ActiveProfileTextBlock.Text)
        {
            Owner = this,
            SelectedWorldFolder = GetSelectedServerWorldFolder()
        };

        bool? result = window.ShowDialog();

        if (result != true)
            return;

        if (window.RequestedAction == "Save")
        {
            ProfileNameTextBox.Text = window.ProfileName;
            SaveCurrentProfile_Click(sender, e);
            return;
        }

        if (window.RequestedAction == "Load")
        {
            string profileName = window.SelectedProfile ?? window.ProfileName;

            ApplySavedServerSettingsForProfile(profileName);

            TryAutoLoadServerDescription();

            ApplyWorldPresetForProfile(profileName);

            ActiveProfileTextBlock.Text = $"Active profile: {profileName}";

            RefreshProfilesList();
            RefreshImportedServerWorlds();
            SelectCurrentWorldFromJson();
            RefreshServerSummary();
        }
        if (window.RequestedAction == "Delete")
        {
            ProfilesComboBox.SelectedItem = window.SelectedProfile;
            DeleteSelectedProfile_Click(sender, e);
        }
    }

    private void RefreshProfiles_Click(object sender, RoutedEventArgs e)
    {
        RefreshProfilesList();
    }

    private void RefreshProfilesList()
    {
        Directory.CreateDirectory(_profilesRoot);

        ProfilesComboBox.Items.Clear();

        foreach (string file in Directory.GetFiles(_profilesRoot, "*.json"))
        {
            ProfilesComboBox.Items.Add(Path.GetFileNameWithoutExtension(file));
        }

        Log($"Profiles refreshed. Found {ProfilesComboBox.Items.Count} profile(s).");
    }

    private void SaveCurrentProfile_Click(object sender, RoutedEventArgs e)
    {
        string profileName = ProfileNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(profileName))
        {
            WpfMessageBox.Show(
                "Enter a profile name first.",
                "Missing Profile Name"
                
            );
            return;
        }

        string safeName = MakeSafeFileName(profileName);
        string sourceJson = GetServerDescriptionJsonPath();
        string profilePath = Path.Combine(_profilesRoot, safeName + ".json");

        try
        {
            if (!File.Exists(sourceJson))
                throw new FileNotFoundException("Active ServerDescription.json was not found.", sourceJson);

            File.Copy(sourceJson, profilePath, overwrite: true);

            Log($"Saved profile: {profilePath}");
            RefreshProfilesList();
        }
        catch (Exception ex)
        {
            Log($"ERROR saving profile: {ex.Message}");
        }
    }

    private void LoadSelectedProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesComboBox.SelectedItem == null)
        {
            ThemedMessageWindow.Show(
              this,
              "No Profile Selected",
              "Select a profile first."
            );
            return;
        }

        string profileName = ProfilesComboBox.SelectedItem.ToString() ?? "";
        string profilePath = Path.Combine(_profilesRoot, profileName + ".json");
        string activeJson = GetServerDescriptionJsonPath();

        try
        {
            if (!File.Exists(profilePath))
                throw new FileNotFoundException("Selected profile was not found.", profilePath);

            ServerDescriptionService.Backup(activeJson, _backupsRoot);
            File.Copy(profilePath, activeJson, overwrite: true);

            ActiveProfileTextBlock.Text = $"Active profile: {profileName}";

            ApplySavedServerSettingsForProfile(profileName);
            ApplyWorldPresetForProfile(profileName);

            Log($"Loaded profile into active server JSON: {profileName}");

            LoadServerDescription_Click(sender, e);
        }
        catch (Exception ex)
        {
            Log($"ERROR loading profile: {ex.Message}");
        }
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }
    private void ProfilesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProfilesComboBox.SelectedItem == null)
            return;

        string profileName = ProfilesComboBox.SelectedItem.ToString() ?? "";

        ProfileNameTextBox.Text = profileName;
    }

    private void DeleteSelectedProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesComboBox.SelectedItem == null)
        {
            WpfMessageBox.Show(
                "Select a profile first.",
                "No Profile Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        string profileName = ProfilesComboBox.SelectedItem.ToString() ?? "";
        string profilePath = Path.Combine(_profilesRoot, profileName + ".json");

        MessageBoxResult result = WpfMessageBox.Show(
            $"Delete this profile?\n\n{profileName}",
            "Delete Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
                Log($"Deleted profile: {profileName}");
            }

            ProfileNameTextBox.Clear();
            RefreshProfilesList();
        }
        catch (Exception ex)
        {
            Log($"ERROR deleting profile: {ex.Message}");
        }
    }

    private void SetupWorldBackupTimer()
    {
        _worldBackupTimer.Interval = TimeSpan.FromHours(1);
        _worldBackupTimer.Tick += WorldBackupTimer_Tick;
        _worldBackupTimer.Start();

        Log("World backup timer started.");
    }

    private void WorldBackupTimer_Tick(object? sender, EventArgs e)
    {
        if (AutoWorldBackupCheckBox.IsChecked != true)
            return;

        try
        {
            string worldId = WorldIslandIdTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(worldId))
            {
                Log("Hourly world backup skipped. No active world selected.");
                return;
            }

            WorldBackupsWindow.BackupWorld(
                _worldBackupsRoot,
                GetCurrentServerFolder(),
                ServerNameTextBox.Text.Trim(),
                worldId
            );

            Log($"Hourly world backup completed for world: {worldId}");
        }
        catch (Exception ex)
        {
            Log($"ERROR during hourly world backup: {ex.Message}");
        }
    }

    private void SetupServerLogTimer()
    {
        _serverLogTimer.Interval = TimeSpan.FromSeconds(2);
        _serverLogTimer.Tick += ServerLogTimer_Tick;
        _serverLogTimer.Start();

        Log("Server log monitor started.");
    }

    private void ServerLogTimer_Tick(object? sender, EventArgs e)
    {
        UpdateServerLogView();
    }

    private string GetServerLogsFolder()
    {
        return Path.Combine(
            GetCurrentServerFolder(),
            "R5",
            "Saved",
            "Logs"
        );
    }

    private string? FindNewestServerLogFile()
    {
        string logsFolder = GetServerLogsFolder();

        if (!Directory.Exists(logsFolder))
            return null;

        return Directory.GetFiles(logsFolder, "*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private void RebuildLivePlayersFromNewestLog()
    {
        try
        {
            string? newestLogFile = FindNewestServerLogFile();

            if (newestLogFile == null || !File.Exists(newestLogFile))
            {
                RefreshLivePlayersUi();
                return;
            }

            string[] lines = File.ReadAllLines(newestLogFile);

            _onlinePlayers.Clear();
            _sessionJoinCount = 0;
            _sessionLeaveCount = 0;

            foreach (string line in lines)
            {
                TryParsePlayerLine(line);
            }

            RefreshLivePlayersUi();

            Log($"Rebuilt live players from newest log: {_onlinePlayers.Count} online.");
        }
        catch (Exception ex)
        {
            Log($"ERROR rebuilding live players from log: {ex.Message}");
        }
    }
    private void ParseServerLogForPlayers(string newLogText)
    {
        if (string.IsNullOrWhiteSpace(newLogText))
            return;

        string[] lines = newLogText.Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries
        );

        foreach (string line in lines)
        {
            TryParsePlayerLine(line);
        }

        RefreshLivePlayersUi();
    }

    private void TryParsePlayerLine(string line)
    {
        // Windrose join:
        // LogNet: Join succeeded: Torment
        const string joinMarker = "Join succeeded:";

        int joinIndex = line.IndexOf(joinMarker, StringComparison.OrdinalIgnoreCase);

        if (joinIndex >= 0)
        {
            string name = line[(joinIndex + joinMarker.Length)..].Trim();

            if (!string.IsNullOrWhiteSpace(name) && _onlinePlayers.Add(name))
            {
                _sessionJoinCount++;
                Log($"Player joined: {name}");
            }

            return;
        }

        // Windrose disconnect block:
        // Account disconnected...
        // Name 'Torment'
        if (line.Contains("Account disconnected", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("MoveAccountToListOfDisconnected", StringComparison.OrdinalIgnoreCase))
        {
            string? name = ExtractQuotedValue(line, "Name");

            if (!string.IsNullOrWhiteSpace(name) && _onlinePlayers.Remove(name))
            {
                _sessionLeaveCount++;
                Log($"Player left: {name}");
            }

            return;
        }

        // Extra fallback:
        // PlayerDisconnected lines often only have session ID, not player name.
    }
    private static string? ExtractQuotedValue(string line, string key)
    {
        string marker = key + " '";

        int start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (start < 0)
            return null;

        start += marker.Length;

        int end = line.IndexOf('\'', start);

        if (end <= start)
            return null;

        return line[start..end].Trim();
    }

    private static string? ExtractPlayerName(string line)
    {
        // Looks for quoted names first:
        // Player "Name" joined
        int firstQuote = line.IndexOf('"');

        if (firstQuote >= 0)
        {
            int secondQuote = line.IndexOf('"', firstQuote + 1);

            if (secondQuote > firstQuote)
                return line[(firstQuote + 1)..secondQuote].Trim();
        }

        // Fallback: take text after common words.
        string[] markers =
        [
            "player",
        "user",
        "client"
        ];

        foreach (string marker in markers)
        {
            int index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
                continue;

            string after = line[(index + marker.Length)..]
                .Replace("joined", "", StringComparison.OrdinalIgnoreCase)
                .Replace("connected", "", StringComparison.OrdinalIgnoreCase)
                .Replace("left", "", StringComparison.OrdinalIgnoreCase)
                .Replace("disconnected", "", StringComparison.OrdinalIgnoreCase)
                .Replace(":", "")
                .Trim();

            if (!string.IsNullOrWhiteSpace(after))
                return after;
        }

        return null;
    }

    private void RefreshLivePlayersUi()
    {
        OnlinePlayersCountTextBlock.Text = $"Online: {_onlinePlayers.Count}";
        PlayerSessionStatsTextBlock.Text = $"Joins: {_sessionJoinCount} | Leaves: {_sessionLeaveCount}";

        OnlinePlayersListBox.Items.Clear();

        foreach (string player in _onlinePlayers.OrderBy(name => name))
        {
            OnlinePlayersListBox.Items.Add(player);
        }
    }
    private void UpdateServerLogView()
    {
        try
        {
            string? newestLogFile = FindNewestServerLogFile();

            if (newestLogFile == null)
            {
                if (IsServerRunning())
                    ServerLogTextBox.Text = "Server is running, but no log file was found yet.";

                return;
            }

            if (!string.Equals(_currentServerLogFile, newestLogFile, StringComparison.OrdinalIgnoreCase))
            {
                _currentServerLogFile = newestLogFile;
                _lastServerLogPosition = 0;
                ServerLogTextBox.Clear();

                ServerLogTextBox.AppendText($"Watching server log:{Environment.NewLine}{newestLogFile}{Environment.NewLine}{Environment.NewLine}");
            }

            using FileStream stream = new(
                newestLogFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
            );

            if (_lastServerLogPosition > stream.Length)
                _lastServerLogPosition = 0;

            stream.Seek(_lastServerLogPosition, SeekOrigin.Begin);

            using StreamReader reader = new(stream);

            string newText = reader.ReadToEnd();

            _lastServerLogPosition = stream.Position;

            if (string.IsNullOrWhiteSpace(newText))
                return;

            ParseServerLogForPlayers(newText);

            ServerLogTextBox.AppendText(newText);
            ServerLogTextBox.ScrollToEnd();

            TrimServerLogTextBox();
        }
        catch (Exception ex)
        {
            ServerLogTextBox.Text = $"Server log read error: {ex.Message}";
        }
    }

    private void TrimServerLogTextBox()
    {
        const int maxCharacters = 30000;

        if (ServerLogTextBox.Text.Length <= maxCharacters)
            return;

        ServerLogTextBox.Text = ServerLogTextBox.Text[^maxCharacters..];
        ServerLogTextBox.ScrollToEnd();
    }

    private async Task EnsureSteamCmdInstalledAsync()
    {
        string steamCmdExe = GetSteamCmdExePath();

        if (File.Exists(steamCmdExe))
        {
            Log("SteamCMD already installed.");
            return;
        }

        string steamCmdFolder = GetSteamCmdFolder();
        Directory.CreateDirectory(steamCmdFolder);

        string zipPath = Path.Combine(steamCmdFolder, "steamcmd.zip");

        Log("Downloading SteamCMD...");

        using HttpClient client = new();
        byte[] data = await client.GetByteArrayAsync(SteamCmdZipUrl);
        await File.WriteAllBytesAsync(zipPath, data);

        Log("Extracting SteamCMD...");

        ZipFile.ExtractToDirectory(
            zipPath,
            steamCmdFolder,
            overwriteFiles: true
        );

        File.Delete(zipPath);

        Log("SteamCMD installed.");
    }
    private string? GetInstalledSteamBuildId()
    {
        string manifestPath = Path.Combine(
            GetCurrentServerFolder(),
            "steamapps",
            $"appmanifest_{WindroseDedicatedServerAppId}.acf"
        );

        if (!File.Exists(manifestPath))
        {
            Log($"Installed manifest not found: {manifestPath}");
            return null;
        }

        foreach (string line in File.ReadAllLines(manifestPath))
        {
            if (!line.Contains("\"buildid\"", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] parts = line.Split('"', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
                return parts.Last().Trim();
        }

        return null;
    }

    private string? ExtractLatestSteamBuildId(string appInfoOutput)
    {
        string[] lines = appInfoOutput.Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries
        );

        foreach (string line in lines)
        {
            if (!line.Contains("\"buildid\"", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] parts = line.Split('"', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
                return parts.Last().Trim();
        }

        return null;
    }
    private async Task CheckSteamServerUpdateStatusOnlyAsync()
    {
        try
        {
            ServerUpdateStatusTextBlock.Text = "Update status: Checking...";

            await EnsureSteamCmdInstalledAsync();

            string installedBuildId = GetInstalledSteamBuildId() ?? "Not installed";
            string steamCmdExe = GetSteamCmdExePath();

            string output = await RunHiddenCommandAsync(
                steamCmdExe,
                $"+login anonymous +app_info_update 1 +app_info_print {WindroseDedicatedServerAppId} +quit"
            );

            string latestBuildId = ExtractLatestSteamBuildId(output) ?? "Unknown";

            if (installedBuildId == "Not installed")
            {
                ServerUpdateStatusTextBlock.Text = $"Update status: Server not installed. Latest build: {latestBuildId}";
                return;
            }

            if (!string.Equals(installedBuildId, latestBuildId, StringComparison.OrdinalIgnoreCase))
            {
                ServerUpdateStatusTextBlock.Text = $"Update available: {installedBuildId} → {latestBuildId}";
                return;
            }

            ServerUpdateStatusTextBlock.Text = $"Update status: Up to date. Build {installedBuildId}";
        }
        catch (Exception ex)
        {
            ServerUpdateStatusTextBlock.Text = "Update status: Check failed";
            Log($"ERROR auto-checking server update: {ex.Message}");
        }
    }
    private async Task CheckSteamServerUpdateAsync()
    {
        try
        {
            await EnsureSteamCmdInstalledAsync();

            string installedBuildId = GetInstalledSteamBuildId() ?? "Not installed";

            string steamCmdExe = GetSteamCmdExePath();

            string output = await RunHiddenCommandAsync(
                steamCmdExe,
                $"+login anonymous +app_info_update 1 +app_info_print {WindroseDedicatedServerAppId} +quit"
            );

            string latestBuildId = ExtractLatestSteamBuildId(output) ?? "Unknown";

            Log($"Installed Windrose server build: {installedBuildId}");
            Log($"Latest Steam build: {latestBuildId}");

            if (installedBuildId == "Not installed")
            {
                WpfMessageBox.Show(
                    $"Windrose Dedicated Server is not installed yet.\n\nLatest Steam build: {latestBuildId}",
                    "Server Not Installed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            if (!string.Equals(installedBuildId, latestBuildId, StringComparison.OrdinalIgnoreCase))
            {
                MessageBoxResult result = WpfMessageBox.Show(
                    $"Update available.\n\nInstalled: {installedBuildId}\nLatest: {latestBuildId}\n\nUpdate now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );

                if (result == MessageBoxResult.Yes)
                    await InstallOrUpdateServerWithSteamCmdAsync();

                return;
            }

            WpfMessageBox.Show(
                $"Server is up to date.\n\nInstalled: {installedBuildId}\nLatest: {latestBuildId}",
                "Server Up To Date",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            Log($"ERROR checking Steam update: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Update Check Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
    private async Task GenerateDefaultServerDescriptionIfMissingAsync()
    {
        string jsonPath = GetServerDescriptionJsonPath();

        if (File.Exists(jsonPath))
            return;

        string exePath = Path.Combine(
            GetCurrentServerFolder(),
            "R5",
            "Binaries",
            "Win64",
            "WindroseServer-Win64-Shipping.exe"
        );

        if (!File.Exists(exePath))
        {
            Log($"Cannot generate default config. Server EXE missing: {exePath}");
            return;
        }

        Log("ServerDescription.json missing. Starting server briefly to generate default config...");

        Process? setupProcess = Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "-log",
            WorkingDirectory = GetCurrentServerFolder(),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        for (int i = 0; i < 60; i++)
        {
            if (File.Exists(jsonPath))
                break;

            await Task.Delay(1000);
        }

        if (setupProcess != null && !setupProcess.HasExited)
        {
            setupProcess.CloseMainWindow();

            if (!setupProcess.WaitForExit(5000))
                setupProcess.Kill(entireProcessTree: true);
        }

        if (File.Exists(jsonPath))
        {
            Log("Default ServerDescription.json generated successfully.");
        }
        else
        {
            Log("Default ServerDescription.json was not generated within timeout.");
        }
    }
    private string BackupWorldsAndServerDescription()
    {
        string jsonPath = GetServerDescriptionJsonPath();
        string worldsFolder = WorldImportService.GetServerWorldsFolder(GetCurrentServerFolder());

        string backupName = $"WorldAndDescriptionBackup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        string backupFolder = Path.Combine(_backupsRoot, backupName);

        Directory.CreateDirectory(backupFolder);

        if (File.Exists(jsonPath))
        {
            File.Copy(
                jsonPath,
                Path.Combine(backupFolder, "ServerDescription.json"),
                overwrite: true
            );

            Log($"Backed up ServerDescription.json to: {backupFolder}");
        }
        else
        {
            Log($"ServerDescription.json not found, skipped JSON backup: {jsonPath}");
        }

        if (Directory.Exists(worldsFolder))
        {
            string destinationWorldsFolder = Path.Combine(backupFolder, "Worlds");

            CopyDirectory(worldsFolder, destinationWorldsFolder);

            Log($"Backed up Worlds folder to: {destinationWorldsFolder}");
        }
        else
        {
            Log($"Worlds folder not found, skipped world backup: {worldsFolder}");
        }

        TrimOldBackupFolders("WorldAndDescriptionBackup_", keepCount: 5);

        return backupFolder;
    }
    

    private void TrimOldBackupFolders(string prefix, int keepCount)
    {
        List<string> folders = Directory.GetDirectories(_backupsRoot)
            .Where(folder => Path.GetFileName(folder).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .ToList();

        foreach (string oldFolder in folders.Skip(keepCount))
        {
            Directory.Delete(oldFolder, recursive: true);
            Log($"Deleted old backup folder: {oldFolder}");
        }
    }
    private void RestoreWorldsAndMergeServerDescription(string backupFolder)
    {
        string activeJsonPath = GetServerDescriptionJsonPath();
        string backupJsonPath = Path.Combine(backupFolder, "ServerDescription.json");

        string activeWorldsFolder = WorldImportService.GetServerWorldsFolder(GetCurrentServerFolder());
        string backupWorldsFolder = Path.Combine(backupFolder, "Worlds");

        // Restore Worlds folder
        if (Directory.Exists(backupWorldsFolder))
        {
            if (Directory.Exists(activeWorldsFolder))
                Directory.Delete(activeWorldsFolder, recursive: true);

            CopyDirectory(backupWorldsFolder, activeWorldsFolder);

            Log($"Restored Worlds folder from backup: {backupWorldsFolder}");
        }

        // Merge JSON
        if (File.Exists(activeJsonPath) && File.Exists(backupJsonPath))
        {
            ServerDescriptionRoot newJson = ServerDescriptionService.Load(activeJsonPath);
            ServerDescriptionRoot oldJson = ServerDescriptionService.Load(backupJsonPath);

            // Keep NEW:
            // newJson.Version
            // newJson.DeploymentId

            // Restore OLD persistent settings:
            newJson.ServerDescription_Persistent = oldJson.ServerDescription_Persistent;

            ServerDescriptionService.Save(activeJsonPath, newJson);

            Log("Merged old persistent server settings into updated ServerDescription.json.");
            Log($"Kept updated DeploymentId: {newJson.DeploymentId}");
        }
        else
        {
            Log("JSON merge skipped. Active or backup ServerDescription.json was missing.");
        }
    }
    private async Task InstallOrUpdateServerWithSteamCmdAsync()
    {
        try
        {
            if (IsServerRunning())
            {
                MessageBoxResult stopResult = WpfMessageBox.Show(
                    "The server is currently running.\n\nStop it before updating?",
                    "Server Running",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (stopResult != MessageBoxResult.Yes)
                    return;

                StopServer_Click(this, new RoutedEventArgs());
            }

            await EnsureSteamCmdInstalledAsync();

            string steamCmdExe = GetSteamCmdExePath();

            if (!File.Exists(steamCmdExe))
                throw new FileNotFoundException("SteamCMD was not found after install.", steamCmdExe);

            Directory.CreateDirectory(GetCurrentServerFolder());

            string arguments =
                $"+force_install_dir \"{GetCurrentServerFolder()}\" " +
                "+login anonymous " +
                $"+app_update {WindroseDedicatedServerAppId} validate " +
                "+quit";
            string backupFolder = BackupWorldsAndServerDescription();
            Log("Starting SteamCMD install/update...");
            Log($"SteamCMD path: {steamCmdExe}");
            Log($"Install folder: {GetCurrentServerFolder()}");

            string output = await RunHiddenCommandAsync(steamCmdExe, arguments);

            if (output.Contains("Success! App", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("fully installed", StringComparison.OrdinalIgnoreCase))
            {
                CopiedServerPathTextBox.Text = GetCurrentServerFolder();

                await GenerateDefaultServerDescriptionIfMissingAsync();

                RestoreWorldsAndMergeServerDescription(backupFolder);

                WorldImportService.MigrateWorldsToNewestRoot(GetCurrentServerFolder(), Log);

                TryAutoLoadServerDescription();
                RefreshImportedServerWorlds();
                SelectCurrentWorldFromJson();
                ApplyLockedWorldIfAvailable();
                RefreshServerSummary();

                WpfMessageBox.Show(
                    "Windrose Dedicated Server installed/updated successfully.",
                    "SteamCMD Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                Log("SteamCMD install/update completed successfully.");
                return;
            }

            WpfMessageBox.Show(
                "SteamCMD finished, but success was not detected.\n\nCheck the App Log output.",
                "SteamCMD Finished",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        catch (Exception ex)
        {
            Log($"ERROR installing/updating with SteamCMD: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "SteamCMD Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
    private async void CopyOrImportServer_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadWithSteamCmdRadioButton.IsChecked == true)
        {
            await InstallOrUpdateServerWithSteamCmdAsync();
            return;
        }

        if (ImportExistingFolderRadioButton.IsChecked == true)
        {
            BrowseAndCopyServer_Click(sender, e);
            return;
        }

        WpfMessageBox.Show(
            "Select Download with SteamCMD or Import existing folder first.",
            "No Source Selected",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
    }

    private void OpenTailscaleSetup_Click(object sender, RoutedEventArgs e)
    {
        TailscaleSetupWindow window = new()
        {
            Owner = this
        };

        window.ShowDialog();

        Log("Opened Tailscale setup window.");
    }

    private void HaveTailscale_Click(object sender, RoutedEventArgs e)
    {
        WpfMessageBox.Show(
            "Good. Make sure Tailscale is installed, logged in, and this PC is visible in your Tailscale admin panel.\n\nNext step: set the Direct Connection Server Port, then use Launch Tailscale Funnel.",
            "Tailscale Ready",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    private void NeedTailscale_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = WpfMessageBox.Show(
            "This will open the Tailscale website so you can create an account and set up your machine.\n\nRecommended: sign in with Google, Microsoft, GitHub, or another provider you trust.\n\nIf you do not trust this button, manually go to:\n\ntailscale.com\n\nThen download the Windows app and sign in.\n\nOpen Tailscale now?",
            "Get Tailscale",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information
        );

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            Process process = new();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c tailscale funnel",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            string combined = output + Environment.NewLine + error;

            if (combined.Contains("funnel"))
            {
                WpfMessageBox.Show(
                    "Tailscale Funnel activation request completed.\n\nIf approval was required, your browser should now show the activation page.",
                    "Funnel Activation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                WpfMessageBox.Show(
                    combined,
                    "Tailscale Output",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                ex.Message,
                "Funnel Activation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        Log("Opened Tailscale Windows download page.");
    }

    private void TryLaunchTailscaleApp()
    {
        string[] possiblePaths =
        [
            Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Tailscale",
            "tailscale-ipn.exe"
        ),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tailscale",
            "tailscale-ipn.exe"
        )
        ];

        foreach (string path in possiblePaths)
        {
            if (!File.Exists(path))
                continue;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            Log($"Launched Tailscale app: {path}");
            return;
        }

        Log("Tailscale app GUI was not found. Continuing with tailscale CLI.");
    }

    private async Task<string> RunHiddenCommandAsync(string fileName, string arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new()
        {
            StartInfo = startInfo
        };

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string combined = output;

        if (!string.IsNullOrWhiteSpace(error))
            combined += Environment.NewLine + error;

        Log($"Command: {fileName} {arguments}");
        Log(combined.Trim());

        return combined.Trim();
    }

    private static string? ExtractTailscaleUrl(string text)
    {
        string marker = "https://";

        int start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (start < 0)
            return null;

        int end = text.IndexOfAny([' ', '\r', '\n', '\t', '"'], start);

        if (end < 0)
            end = text.Length;

        return text[start..end].Trim();
    }
    private async void LaunchTailscaleFunnel_Click(object sender, RoutedEventArgs e)
    {
        string portText = DirectConnectionServerPortTextBox.Text.Trim();

        if (!int.TryParse(portText, out int port) || port <= 0)
        {
            string? input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the Windrose direct connection server port:",
                "Direct Connection Port",
                "7777"
            );

            if (!int.TryParse(input, out port) || port <= 0)
            {
                WpfMessageBox.Show(
                    "Valid port number required.",
                    "Missing Port",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            DirectConnectionServerPortTextBox.Text = port.ToString();
        }

        try
        {
            Log("Launching Tailscale app...");

            TryLaunchTailscaleApp();

            await Task.Delay(2500);

            Log("Starting Tailscale connection...");

            await RunHiddenCommandAsync("tailscale", "up");

            Log("Resetting old Tailscale Funnel config...");

            await RunHiddenCommandAsync(
                "tailscale",
                "funnel reset"
            );

            Log($"Starting Tailscale Funnel on port {port}...");

            string funnelOutput = await RunHiddenCommandAsync(
                "tailscale",
                $"funnel --bg {port}"
            );

            string statusOutput = await RunHiddenCommandAsync(
                    "tailscale",
                    "funnel status --json"
            );

            string combinedOutput = funnelOutput + Environment.NewLine + statusOutput;

            string? url = ExtractTailscaleUrl(combinedOutput);

            if (!string.IsNullOrWhiteSpace(url))
            {
                DirectConnectionServerAddressTextBox.Text = url;

                SaveServerDescription_Click(sender, e);
                RefreshServerSummary();

                WpfMessageBox.Show(
                    $"Tailscale Funnel started.\n\nAddress saved:\n{url}\n\nUse Direct Connection was not changed.",
                    "Tailscale Funnel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            WpfMessageBox.Show(
                "Tailscale Funnel command ran, but no public URL was detected.\n\nCheck the App Log output. You may need to approve Funnel in the Tailscale admin console.",
                "Tailscale Funnel",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        catch (Exception ex)
        {
            Log($"ERROR launching Tailscale Funnel: {ex.Message}");

            WpfMessageBox.Show(
                ex.Message,
                "Tailscale Funnel Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void LoadAppSettings()
    {
        try
        {
            AppSettings settings = AppSettingsService.Load(_appSettingsPath);

            VisibleConsoleRadioButton.IsChecked = settings.LaunchModeIndex == 0;
            HiddenWindowRadioButton.IsChecked = settings.LaunchModeIndex == 1;
            AutoRestartCheckBox.IsChecked = settings.AutoRestart;
            AutoWorldBackupCheckBox.IsChecked = settings.AutoWorldBackup;
            _lockedWorldIslandId = settings.LockedWorldIslandId;
            Log("Loaded app settings.");
        }
        catch (Exception ex)
        {
            Log($"ERROR loading app settings: {ex.Message}");
        }
    }

    private void SaveAppSettings()
    {
        try
        {
            AppSettings existingSettings = AppSettingsService.Load(_appSettingsPath);

            AppSettings settings = new()
            {
                LaunchModeIndex = HiddenWindowRadioButton.IsChecked == true ? 1 : 0,
                AutoRestart = AutoRestartCheckBox.IsChecked == true,
                AutoWorldBackup = AutoWorldBackupCheckBox.IsChecked == true,
                LockedWorldIslandId = _lockedWorldIslandId,

                // Preserve settings saved by other features
                LastActiveWorldPreset = existingSettings.LastActiveWorldPreset,
                SkippedAppVersion = existingSettings.SkippedAppVersion,
                DonationPopupDisabled = existingSettings.DonationPopupDisabled
            };

            AppSettingsService.Save(_appSettingsPath, settings);

            Log("Saved app settings.");
        }
        catch (Exception ex)
        {
            Log($"ERROR saving app settings: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveAppSettings();
        base.OnClosed(e);
    }
    private async void CheckUpdateServer_Click(object sender, RoutedEventArgs e)
    {
        await CheckSteamServerUpdateAsync();
    }
    private async void CheckAppUpdate_Click(object sender, RoutedEventArgs e)
    {
        await CheckAppUpdateAsync();
    }
    private void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";

        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();

        try
        {
            string logFile = Path.Combine(_logsRoot, "servercontrol.log");
            File.AppendAllText(logFile, line + Environment.NewLine);
        }
        catch
        {
            // Do not crash UI if logging fails.
        }
    }
}