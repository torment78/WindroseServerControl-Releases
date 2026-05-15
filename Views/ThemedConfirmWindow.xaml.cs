using System.Windows;
using System.Windows.Input;

namespace Elka_windrose_server_control.Views
{
    public partial class ThemedConfirmWindow : Window
    {
        public bool Result { get; private set; }

        public ThemedConfirmWindow(string title, string message)
        {
            InitializeComponent();

            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
        }

        public static bool Show(Window owner, string title, string message)
        {
            ThemedConfirmWindow window = new(title, message)
            {
                Owner = owner
            };

            window.ShowDialog();

            return window.Result;
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}