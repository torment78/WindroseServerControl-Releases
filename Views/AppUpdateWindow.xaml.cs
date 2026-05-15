using System.Windows;
using System.Windows.Input;

namespace Elka_windrose_server_control.Views
{
    public partial class AppUpdateWindow : Window
    {
        public bool ShouldInstall { get; private set; }
        public bool ShouldSkipVersion { get; private set; }

        public AppUpdateWindow(string currentVersion, string latestVersion, string notes)
        {
            InitializeComponent();

            VersionTextBlock.Text = $"Current: {currentVersion}   →   Latest: {latestVersion}";
            NotesTextBlock.Text = string.IsNullOrWhiteSpace(notes)
                ? "No release notes were provided."
                : notes;
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            ShouldInstall = true;
            DialogResult = true;
            Close();
        }

        private void Later_Click(object sender, RoutedEventArgs e)
        {
            ShouldInstall = false;
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        public void SetDownloadProgress(double percent, string status)
        {
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = percent;
            DownloadStatusTextBlock.Text = status;
        }
        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            ShouldSkipVersion = true;
            ShouldInstall = false;
            DialogResult = true;
            Close();
        }
    }
}