using System.Diagnostics;
using System.Windows;

namespace Elka_windrose_server_control.Views;

public partial class DonationWindow : Window
{
    private readonly string _donationUrl;

    public bool DisableFuturePopups => AlreadyDonatedCheckBox.IsChecked == true;

    public DonationWindow(string donationUrl)
    {
        InitializeComponent();
        _donationUrl = donationUrl;
    }

    private void OpenDonation_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _donationUrl,
            UseShellExecute = true
        });

        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
}