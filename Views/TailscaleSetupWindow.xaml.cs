using System.Diagnostics;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace Elka_windrose_server_control.Views;

public partial class TailscaleSetupWindow : Window
{
    public TailscaleSetupWindow()
    {
        InitializeComponent();
    }

    private void OpenLogin_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://login.tailscale.com/start");
    }

    private void DownloadApp_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://tailscale.com/download/windows");
    }

    private void ActivateFunnel_Click(object sender, RoutedEventArgs e)
    {
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

            string combined = (output + Environment.NewLine + error).Trim();

            // -------------------------------------------------
            // FUNNEL ALREADY ENABLED
            // -------------------------------------------------
            if (combined.Contains("Available on the internet") ||
                combined.Contains("https://"))
            {
                WpfMessageBox.Show(
                    "Tailscale Funnel is already enabled and working.",
                    "Funnel Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            // -------------------------------------------------
            // APPROVAL PAGE OPENED
            // -------------------------------------------------
            if (combined.Contains("To access this machine") ||
                combined.Contains("Visit:") ||
                combined.Contains("log in"))
            {
                WpfMessageBox.Show(
                    "Tailscale opened the browser approval flow.\n\nApprove Funnel access in your browser.",
                    "Browser Approval Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            // -------------------------------------------------
            // TAILSCALE NOT INSTALLED
            // -------------------------------------------------
            if (combined.Contains("'tailscale' is not recognized") ||
                combined.Contains("not recognized as an internal"))
            {
                WpfMessageBox.Show(
                    "Tailscale CLI was not found.\n\nInstall the Tailscale Windows application first.",
                    "Tailscale Missing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            // -------------------------------------------------
            // NOT LOGGED IN
            // -------------------------------------------------
            if (combined.Contains("not logged in") ||
                combined.Contains("NeedsLogin"))
            {
                WpfMessageBox.Show(
                    "Tailscale is installed but not logged in.\n\nOpen Tailscale and sign into your account first.",
                    "Login Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            // -------------------------------------------------
            // ADMIN RIGHTS
            // -------------------------------------------------
            if (combined.Contains("Access is denied") ||
                combined.Contains("administrator"))
            {
                WpfMessageBox.Show(
                    "Administrator rights may be required.\n\nTry launching this application as Administrator.",
                    "Administrator Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            // -------------------------------------------------
            // UNKNOWN OUTPUT
            // -------------------------------------------------
            WpfMessageBox.Show(
                combined,
                "Tailscale Funnel Output",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
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
    }
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}