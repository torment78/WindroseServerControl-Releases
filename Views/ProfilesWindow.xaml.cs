using System.IO;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace Elka_windrose_server_control.Views;

public partial class ProfilesWindow : Window
{
    private readonly string _profilesRoot;
    public string? SelectedWorldFolder { get; set; }
    public string ProfileName => ProfileNameTextBox.Text.Trim();
    public string? SelectedProfile => ProfilesComboBox.SelectedItem?.ToString();

    public string? RequestedAction { get; private set; }

    public ProfilesWindow(string profilesRoot, string activeProfileText)
    {
        InitializeComponent();

        _profilesRoot = profilesRoot;
        ActiveProfileTextBlock.Text = activeProfileText;

        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        Directory.CreateDirectory(_profilesRoot);

        ProfilesComboBox.Items.Clear();

        foreach (string file in Directory.GetFiles(_profilesRoot, "*.json"))
        {
            ProfilesComboBox.Items.Add(Path.GetFileNameWithoutExtension(file));
        }
    }

    private void ProfilesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProfilesComboBox.SelectedItem == null)
            return;

        ProfileNameTextBox.Text = ProfilesComboBox.SelectedItem.ToString() ?? "";
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            WpfMessageBox.Show(
                "Enter a profile name first.",
                "Missing Profile Name",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return;
        }

        RequestedAction = "Save";
        DialogResult = true;
        Close();
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile == null)
        {
            WpfMessageBox.Show(
                "Select a profile first.",
                "No Profile Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return;
        }

        RequestedAction = "Load";
        DialogResult = true;
        Close();
    }
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile == null)
        {
            ThemedMessageWindow.Show(
                this,
                "No Profile Selected",
                "Select a profile first."
            );

            return;
        }

        MessageBoxResult result = WpfMessageBox.Show(
            $"Delete this profile?\n\n{SelectedProfile}",
            "Delete Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result != MessageBoxResult.Yes)
            return;

        RequestedAction = "Delete";
        DialogResult = true;
        Close();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshProfiles();
    }
    private void ProfilePresets_Click(object sender, RoutedEventArgs e)
    {
        string profileName = ProfileNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(profileName))
        {
            ThemedMessageWindow.Show(
                this,
                "Missing Profile Name",
                "Type a profile name first, then open Profile Presets."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedWorldFolder))
        {
            ThemedMessageWindow.Show(
                this,
                "No World Selected",
                "No world folder was passed into the Profiles window."
            );

            return;
        }

        ProfilePresetWindow window = new(SelectedWorldFolder, profileName)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}