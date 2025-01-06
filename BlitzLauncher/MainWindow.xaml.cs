using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace BlitzLauncher {
    public enum LaunchMode {
        Automatic = 0,
        ForceSteam = 1,
        ForceWGC = 2,
        CustomPath = 3,
        ForceUWP = 4
    }
    public class Settings {
        public string LastDllPath { get; set; } = "";
        public string LastExePath { get; set; } = "";
        public string CustomExePath { get; set; } = "";
        
        public LaunchMode LaunchMode { get; set; } = LaunchMode.Automatic;
    }

    public partial class MainWindow : Window {
        internal Settings injectorSettings = new Settings();
        private static readonly string ConfigFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlitzLoader",
            "settings.json"
        );

        private System.Reflection.Assembly CurrentAssemblyResolve(object sender, ResolveEventArgs e) {
            string log = $"AssemblyResolve: {e.Name}";
            File.AppendAllText("MissingDeps.log", log);
            return null;
        }


        public MainWindow() {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentAssemblyResolve;
            InitializeComponent();
            LoadSettings();
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowState = WindowState.Normal;
            this.MaxHeight = this.Height;
            this.MaxWidth = this.Width;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            var wa = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            this.Left = wa.Left + (wa.Width - this.Width) / 2;
            this.Top = wa.Top + (wa.Height - this.Height) / 2;

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile));
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            string lastDllPath = LoadSettings();
            if (!string.IsNullOrEmpty(lastDllPath)) {
                UpdateDllUi(lastDllPath);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length == 1) {
                    string fileExtension = Path.GetExtension(files[0]);

                    if (fileExtension.Equals(".dll", StringComparison.OrdinalIgnoreCase)) {
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

        private void Window_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length == 1) {
                    string fileExtension = Path.GetExtension(files[0]);

                    if (fileExtension.Equals(".dll", StringComparison.OrdinalIgnoreCase)) {
                        UpdateDllUi(files[0]);
                        injectorSettings.LastDllPath = files[0];
                        SaveSettings();
                    }
                }
            }
            e.Handled = true;
        }

        private void OpenModsFolder_Click(object sender, RoutedEventArgs e) {
            try {
                string modsFolderPath = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\BlitzLoader\Mods");
                if (!Directory.Exists(modsFolderPath)) {
                    Directory.CreateDirectory(modsFolderPath);
                }

                Process.Start("explorer.exe", modsFolderPath);
            } catch (Exception ex) {
                MessageBox.Show($"Could not open mods folder: {ex.Message}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e) {
            SettingsWindow sw = new SettingsWindow(injectorSettings);
            sw.Show();
            SaveSettings();
        }

        private void BrowseDlls_Click(object sender, RoutedEventArgs e) {
            var dllFileDialog = new OpenFileDialog {
                Filter = "Dll Files (*.dll)|*.dll",
                Title = "Select a Dll"
            };
            if (dllFileDialog.ShowDialog() == true) {
                UpdateDllUi(dllFileDialog.FileName);
                injectorSettings.LastDllPath = dllFileDialog.FileName;
                SaveSettings();
            }
        }

        private void UpdateDllUi(string dllFilePath) {
            txtDllPath.Text = dllFilePath;
            txtDllPath.Foreground = new SolidColorBrush(Color.FromRgb(138, 43, 226));
            txtDllPath.FontSize = 13;
            txtDllPath.Padding = new Thickness(3, 10, 0, 0);
            DllPathText.Text = $"Selected Dll: {Path.GetFileName(dllFilePath)}";
            DllPathText.Foreground = new SolidColorBrush(Color.FromRgb(128, 239, 128));

            var glowEf = (DropShadowEffect)txtDllPath.Template.FindName("Glow", txtDllPath);
            glowEf.Color = Color.FromRgb(138, 43, 226);
            glowEf.ShadowDepth = 0;
            glowEf.BlurRadius = 25;
            glowEf.Opacity = 1.0;
        }

        private CancellationTokenSource stillRunningTokenCancellationSource = new CancellationTokenSource();
        private Thread stillRunningThread = null;
        private void CheckIfStillRunning() {
            Process worldOfTonksProcess = Process.GetProcessesByName("wotblitz").FirstOrDefault();
            if (worldOfTonksProcess == null || worldOfTonksProcess.HasExited) {
                return;
            }
            var token = stillRunningTokenCancellationSource.Token;
            while (!token.IsCancellationRequested) {
                if (worldOfTonksProcess.WaitForExit(1)) { // hey process died so do something about it
                    StatusText.Dispatcher.Invoke(new Action(() => {
                        StatusText.Text = "Status: Not Injected!";
                        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                    }));
                    return;
                }
            }
        }

        private int previousInjectPid = 0;
        private async void InjectDll_Click(object sender, RoutedEventArgs e) {
            try {

                if (previousInjectPid != 0 && Process.GetProcessById(previousInjectPid) != null) {
                    MessageBox.Show("DLL is already injected!", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            } catch (ArgumentException) {
                // ignore, process was closed
            }
            Process alreadyStartedInstance = Process.GetProcessesByName("wotblitz").FirstOrDefault();
            int pid = alreadyStartedInstance?.Id ?? 0;
            if (pid != 0) { //There is a started process which we did not open
                injectorSettings.LastExePath = alreadyStartedInstance.MainModule.FileName;
                if (MessageBox.Show("The game needs to be started by the launcher.\nDo you want to close the game now?",
                        "Already started!",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) {
                    return;
                } else {
                    alreadyStartedInstance.Kill();
                }
            }
            // Open the process
            var suspendedProcess = ProcessManager.StartTonkGame(injectorSettings);

            // Inject the dll
            bool injected = Injector.InjectDll(suspendedProcess, injectorSettings.LastDllPath);

            if (!injected) {
                suspendedProcess.Resume();
            } else {
                previousInjectPid = (int)suspendedProcess.ProcessId;
            }

            suspendedProcess.Dispose();
        }

        private void SaveSettings() {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(injectorSettings, options);
            File.WriteAllText(ConfigFile, json);
        }

        private string LoadSettings() {
            if (!File.Exists(ConfigFile)) return null;
            var json = File.ReadAllText(ConfigFile);
            var settings = JsonSerializer.Deserialize<Settings>(json);
            if (settings != null)
                injectorSettings = settings;
            return settings?.LastDllPath;
        }
    }
}


