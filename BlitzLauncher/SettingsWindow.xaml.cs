using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Controls;
using System.Text.Json;
using Microsoft.Win32;

namespace BlitzLauncher {
    public partial class SettingsWindow : Window {
        private Settings _settings;
        private static readonly string ConfigFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlitzLoader",
            "settings.json"
        );
        public SettingsWindow(Settings settings) {
            InitializeComponent();
            _settings = settings;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowState = WindowState.Normal;
            this.MaxHeight = this.Height;
            this.MaxWidth = this.Width;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            var wa = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            this.Left = wa.Left + (wa.Width - this.Width) / 2;
            this.Top = wa.Top + (wa.Height - this.Height) / 2;
            switch (_settings.LaunchMode) {
                case LaunchMode.Automatic:
                    AutoSetting.IsSelected = true;
                    break;
                case LaunchMode.ForceSteam:
                    SteamSetting.IsSelected = true;
                    break;
                case LaunchMode.ForceWGC:
                    WGCSetting.IsSelected = true;
                    break;
                case LaunchMode.ForceUWP:
                    UWPSetting.IsSelected = true;
                    break;
                case LaunchMode.CustomPath:
                    CustomPathSetting.IsSelected = true;
                    break;
            }
        }

        private void LaunchMode_SelectChanged(object sender, SelectionChangedEventArgs e) {
            if (CustomPathSetting.IsSelected) { 
                CustomPathTextBox.Visibility = Visibility.Visible;
                BrowseForPathButton.Visibility = Visibility.Visible;
            } else {
                CustomPathTextBox.Visibility = Visibility.Hidden;
                BrowseForPathButton.Visibility = Visibility.Hidden;
            }
        }
        
        private string LoadSettings() {
            if (!File.Exists(ConfigFile)) return null;
            var json = File.ReadAllText(ConfigFile);
            var settings = JsonSerializer.Deserialize<Settings>(json);
            if (settings != null)
                _settings = settings;
            return settings?.LastDllPath;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e) {
            if (AutoSetting.IsSelected)
                _settings.LaunchMode = LaunchMode.Automatic;
            else if (SteamSetting.IsSelected)
                _settings.LaunchMode = LaunchMode.ForceSteam;
            else if (WGCSetting.IsSelected)
                _settings.LaunchMode = LaunchMode.ForceWGC;
            else if (UWPSetting.IsSelected)
                _settings.LaunchMode = LaunchMode.ForceUWP;
            else if (CustomPathSetting.IsSelected)
                _settings.LaunchMode = LaunchMode.CustomPath;
        }

        private void BrowseForPath_Click(object sender, RoutedEventArgs e) {
            var fileDialog = new OpenFileDialog() {
                Filter = "Executable Files (wotblitz.exe)|wotblitz.exe",
                Title = "Select WoTBlitz Executable",
                CheckFileExists = true
            };

            if (fileDialog.ShowDialog() == true) {
                UpdateDllUi(fileDialog.FileName);
                CustomPathTextBox.Text = fileDialog.FileName;
                _settings.LastExePath = fileDialog.FileName;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length == 1) {
                    string fileName = Path.GetFileName(files[0]);

                    if (fileName.Equals("wotblitz.exe", StringComparison.OrdinalIgnoreCase)) {
                        UpdateDllUi(files[0]);
                        _settings.CustomExePath = files[0];
                    } else {
                        System.Media.SystemSounds.Hand.Play();
                    }
                }
            }
            e.Handled = true;
        }

        private void Window_DragOver(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length == 1) {
                    string fileExtension = Path.GetExtension(files[0]);

                    if (fileExtension.Equals(".exe", StringComparison.OrdinalIgnoreCase)) {
                        e.Effects = DragDropEffects.Copy;
                    } else {
                        e.Effects = DragDropEffects.None;
                    }
                }
            } else {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void UpdateDllUi(string exeFilePath) {
            CustomPathTextBox.Text = exeFilePath;
            CustomPathTextBox.Foreground = new SolidColorBrush(Color.FromRgb(138, 43, 226));
            CustomPathTextBox.FontSize = 13;
            CustomPathTextBox.Padding = new Thickness(3, 5, 0, 0);
            var glowEf = (DropShadowEffect)CustomPathTextBox.Template.FindName("Glow", CustomPathTextBox);
            glowEf.Color = Color.FromRgb(138, 43, 226);
            glowEf.ShadowDepth = 0;
            glowEf.BlurRadius = 25;
            glowEf.Opacity = 1.0;
        }
    }
}
