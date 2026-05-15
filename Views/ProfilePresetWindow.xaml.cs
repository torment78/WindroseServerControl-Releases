using Elka_windrose_server_control.Services;
using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace Elka_windrose_server_control.Views
{
    public partial class ProfilePresetWindow : Window
    {
        private readonly string _worldFolder;
        private readonly string _profileName;
        private bool _isApplyingPreset;

        public ProfilePresetWindow(string worldFolder, string profileName)
        {
            InitializeComponent();

            _worldFolder = worldFolder;
            _profileName = profileName;

            PresetComboBox.SelectedIndex = 1;
            ApplyNormalPreset();
            RefreshValueLabels();

            WorldDescriptionService.EnsureDefaultBackup(_worldFolder);

            string existingPresetPath = WorldDescriptionService.GetProfileWorldDescriptionPath(_worldFolder, _profileName);

            if (File.Exists(existingPresetPath))
            {
                JsonObject existingPreset = WorldDescriptionService.LoadWorldDescription(existingPresetPath);
                ApplyJsonToEditor(existingPreset);
            }
        }

        private void PresetComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isApplyingPreset)
                return;

            string preset = GetComboText(PresetComboBox);

            _isApplyingPreset = true;

            switch (preset)
            {
                case "Easy":
                    ApplyEasyPreset();
                    break;

                case "Normal":
                    ApplyNormalPreset();
                    break;

                case "Hard":
                    ApplyHardPreset();
                    break;

                case "Default":
                    LoadDefaultIntoEditor();
                    break;
            }

            _isApplyingPreset = false;
            RefreshValueLabels();
        }


        private void ApplyEasyPreset()
        {
            CombatDifficultyComboBox.SelectedIndex = 0;

            EnemyHealthSlider.Value = 70;
            EnemyDamageSlider.Value = 60;
            EnemyShipHealthSlider.Value = 70;
            EnemyShipDamageSlider.Value = 60;
            BoardingDifficultySlider.Value = 70;

            CoopEnemyScalingSlider.Value = 100;
            CoopShipScalingSlider.Value = 0;

            ImmersiveExplorationCheckBox.IsChecked = false;
            SharedQuestsCheckBox.IsChecked = true;
        }

        private void ApplyNormalPreset()
        {
            CombatDifficultyComboBox.SelectedIndex = 1;

            EnemyHealthSlider.Value = 100;
            EnemyDamageSlider.Value = 100;
            EnemyShipHealthSlider.Value = 100;
            EnemyShipDamageSlider.Value = 100;
            BoardingDifficultySlider.Value = 100;

            CoopEnemyScalingSlider.Value = 100;
            CoopShipScalingSlider.Value = 0;

            ImmersiveExplorationCheckBox.IsChecked = false;
            SharedQuestsCheckBox.IsChecked = true;
        }

        private void ApplyHardPreset()
        {
            CombatDifficultyComboBox.SelectedIndex = 2;

            EnemyHealthSlider.Value = 150;
            EnemyDamageSlider.Value = 125;
            EnemyShipHealthSlider.Value = 150;
            EnemyShipDamageSlider.Value = 125;
            BoardingDifficultySlider.Value = 150;

            CoopEnemyScalingSlider.Value = 100;
            CoopShipScalingSlider.Value = 0;

            ImmersiveExplorationCheckBox.IsChecked = false;
            SharedQuestsCheckBox.IsChecked = true;
        }

        private void LoadDefaultIntoEditor()
        {
            string defaultPath = WorldDescriptionService.GetDefaultWorldDescriptionPath(_worldFolder);

            if (!System.IO.File.Exists(defaultPath))
                return;

            JsonObject root = WorldDescriptionService.LoadWorldDescription(defaultPath);

            ApplyJsonToEditor(root);
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            string presetType = GetComboText(PresetComboBox);

            if (presetType == "Normal")
                presetType = "Medium";

            if (presetType == "Default")
                presetType = "Custom";

            JsonObject presetRoot = WorldDescriptionService.BuildPresetFromCurrentWorld(
                _worldFolder,
                presetType,
                GetComboText(CombatDifficultyComboBox),
                SharedQuestsCheckBox.IsChecked == true,
                ImmersiveExplorationCheckBox.IsChecked == true,
                EnemyHealthSlider.Value,
                EnemyDamageSlider.Value,
                EnemyShipHealthSlider.Value,
                EnemyShipDamageSlider.Value,
                BoardingDifficultySlider.Value,
                CoopEnemyScalingSlider.Value,
                CoopShipScalingSlider.Value
            );

            WorldDescriptionService.SavePresetToWorldProfile(
                _worldFolder,
                _profileName,
                presetRoot
            );
            if (SaveServerSettingsCheckBox.IsChecked == true)
            {
                string activeServerDescriptionPath = Path.Combine(
                  AppDomain.CurrentDomain.BaseDirectory,
                 "ServerFiles",
                 "Windrose Dedicated Server",
                 "R5",
                 "ServerDescription.json"
                );

                string profileFolder = WorldDescriptionService.GetProfileFolder(_worldFolder, _profileName);
                Directory.CreateDirectory(profileFolder);

                string profileServerDescriptionPath = Path.Combine(profileFolder, "ServerDescription.json");

                if (File.Exists(activeServerDescriptionPath))
                {
                    File.Copy(
                        activeServerDescriptionPath,
                        profileServerDescriptionPath,
                        overwrite: true
                    );
                }
            }
            ThemedMessageWindow.Show(
                this,
                "Preset Saved",
                $"World preset saved into this world for profile:\n\n{_profileName}"
            );
        }

        private void ApplyDefault_Click(object sender, RoutedEventArgs e)
        {
            WorldDescriptionService.RestoreDefaultToWorld(_worldFolder);

            ThemedMessageWindow.Show(
                this,
                "Default Restored",
                "The original WorldDescription.json was restored to this world."
            );
        }

        private void AnyCustomValue_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingPreset)
                return;

            if (PresetComboBox != null)
                PresetComboBox.SelectedIndex = 3;

            RefreshValueLabels();
        }

        private void AnyCustomValue_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isApplyingPreset)
                return;

            if (PresetComboBox != null)
                PresetComboBox.SelectedIndex = 3;

            RefreshValueLabels();
        }

        private void RefreshValueLabels()
        {
            if (EnemyHealthValueTextBlock == null ||
                EnemyHealthSlider == null ||
                EnemyDamageValueTextBlock == null ||
                EnemyDamageSlider == null ||
                EnemyShipHealthValueTextBlock == null ||
                EnemyShipHealthSlider == null ||
                EnemyShipDamageValueTextBlock == null ||
                EnemyShipDamageSlider == null ||
                BoardingDifficultyValueTextBlock == null ||
                BoardingDifficultySlider == null ||
                CoopEnemyScalingValueTextBlock == null ||
                CoopEnemyScalingSlider == null ||
                CoopShipScalingValueTextBlock == null ||
                CoopShipScalingSlider == null)
            {
                return;
            }

            EnemyHealthValueTextBlock.Text = $"{EnemyHealthSlider.Value:0}%";
            EnemyDamageValueTextBlock.Text = $"{EnemyDamageSlider.Value:0}%";
            EnemyShipHealthValueTextBlock.Text = $"{EnemyShipHealthSlider.Value:0}%";
            EnemyShipDamageValueTextBlock.Text = $"{EnemyShipDamageSlider.Value:0}%";
            BoardingDifficultyValueTextBlock.Text = $"{BoardingDifficultySlider.Value:0}%";
            CoopEnemyScalingValueTextBlock.Text = $"{CoopEnemyScalingSlider.Value:0}%";
            CoopShipScalingValueTextBlock.Text = $"{CoopShipScalingSlider.Value:0}%";
        }

        private void ApplyJsonToEditor(JsonObject root)
        {
            _isApplyingPreset = true;

            try
            {
                JsonObject worldDescription = root["WorldDescription"]!.AsObject();
                JsonObject worldSettings = worldDescription["WorldSettings"]!.AsObject();
                JsonObject boolParameters = worldSettings["BoolParameters"]!.AsObject();
                JsonObject floatParameters = worldSettings["FloatParameters"]!.AsObject();
                JsonObject tagParameters = worldSettings["TagParameters"]!.AsObject();

                string presetType = worldDescription["WorldPresetType"]?.ToString() ?? "Custom";

                PresetComboBox.SelectedIndex = presetType switch
                {
                    "Easy" => 0,
                    "Medium" => 1,
                    "Normal" => 1,
                    "Hard" => 2,
                    "Custom" => 3,
                    _ => 3
                };

                string combatTag = tagParameters[Key("WDS.Parameter.CombatDifficulty")]?["TagName"]?.ToString() ?? "";

                CombatDifficultyComboBox.SelectedIndex =
                    combatTag.EndsWith(".Easy", StringComparison.OrdinalIgnoreCase) ? 0 :
                    combatTag.EndsWith(".Normal", StringComparison.OrdinalIgnoreCase) ? 1 :
                    combatTag.EndsWith(".Hard", StringComparison.OrdinalIgnoreCase) ? 2 :
                    1;

                SharedQuestsCheckBox.IsChecked =
                    GetBool(boolParameters, "WDS.Parameter.Coop.SharedQuests", true);

                ImmersiveExplorationCheckBox.IsChecked =
                    GetBool(boolParameters, "WDS.Parameter.EasyExplore", false);

                EnemyHealthSlider.Value =
                    GetPercent(floatParameters, "WDS.Parameter.MobHealthMultiplier", 100);

                EnemyDamageSlider.Value =
                    GetPercent(floatParameters, "WDS.Parameter.MobDamageMultiplier", 100);

                EnemyShipHealthSlider.Value =
                    GetPercent(floatParameters, "WDS.Parameter.ShipsHealthMultiplier", 100);

                EnemyShipDamageSlider.Value =
                    GetPercent(floatParameters, "WDS.Parameter.ShipsDamageMultiplier", 100);

                BoardingDifficultySlider.Value =
                    GetPercent(floatParameters, "WDS.Parameter.BoardingDifficultyMultiplier", 100);

                CoopEnemyScalingSlider.Value =
                    GetPercent(floatParameters, "WDS.Parameter.Coop.StatsCorrectionModifier", 100);

                CoopShipScalingSlider.Value =
                    GetPercent(floatParameters, "WDS.Parameter.Coop.ShipStatsCorrectionModifier", 0);
            }
            finally
            {
                _isApplyingPreset = false;
                RefreshValueLabels();
            }
        }
        private static bool GetBool(JsonObject obj, string tagName, bool fallback)
        {
            JsonNode? node = obj[Key(tagName)];

            if (node == null)
                return fallback;

            return bool.TryParse(node.ToString(), out bool value)
                ? value
                : fallback;
        }

        private static double GetPercent(JsonObject obj, string tagName, double fallback)
        {
            JsonNode? node = obj[Key(tagName)];

            if (node == null)
                return fallback;

            if (!double.TryParse(node.ToString(), out double value))
                return fallback;

            return value * 100.0;
        }

        private static string Key(string tagName)
        {
            return $"{{\"TagName\": \"{tagName}\"}}";
        }

        private static string GetComboText(WpfComboBox comboBox)
        {
            return ((WpfComboBoxItem)comboBox.SelectedItem).Content?.ToString() ?? "";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}