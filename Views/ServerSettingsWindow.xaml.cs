using System.Windows;

namespace Elka_windrose_server_control.Views;

public partial class ServerSettingsWindow : Window
{
    public ServerSettingsWindow()
    {
        InitializeComponent();
    }
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}