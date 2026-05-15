using System.Windows;

namespace Elka_windrose_server_control.Views;

public partial class ThemedMessageWindow : Window
{
    public ThemedMessageWindow(string title, string message)
    {
        InitializeComponent();

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
    }

    public static void Show(Window owner, string title, string message)
    {
        ThemedMessageWindow window = new(title, message)
        {
            Owner = owner
        };

        window.ShowDialog();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
}