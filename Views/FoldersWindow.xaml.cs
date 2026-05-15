using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Elka_windrose_server_control.Views;

public partial class FoldersWindow : Window
{
    private readonly string _copiedServerFolder;
    private readonly string _backupsFolder;
    private readonly string _logsFolder;
    private readonly string _serverLogsFolder;

    public FoldersWindow(
        string copiedServerFolder,
        string backupsFolder,
        string logsFolder,
        string serverLogsFolder)
    {
        InitializeComponent();

        _copiedServerFolder = copiedServerFolder;
        _backupsFolder = backupsFolder;
        _logsFolder = logsFolder;
        _serverLogsFolder = serverLogsFolder;
    }

    private void OpenCopiedServerFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder(_copiedServerFolder);
    }

    private void OpenBackupsFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder(_backupsFolder);
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder(_logsFolder);
    }

    private void OpenServerLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder(_serverLogsFolder);
    }

    private static void OpenFolder(string folderPath)
    {
        Directory.CreateDirectory(folderPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
    }
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}