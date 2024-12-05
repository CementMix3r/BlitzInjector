using System;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlitzInjector2._0 {
    internal class Settings {
        public string LastDllPath { get; set; } = "";
        public string LastExePath { get; set; } = "";
    }

    public partial class MainWindow : Window {
        internal Settings injectorSettings = new Settings();
        private static readonly string ConfigFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlitzLoader",
            "settings.json"
        );

        public MainWindow() {
            InitializeComponent();
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

        private async void InjectDll_Click(object sender, RoutedEventArgs e) {
            Process susProcess = Process.GetProcessesByName("wotblitz").FirstOrDefault();
            int pid = susProcess?.Id ?? 0;
            if (pid == 0) { // try starting the video game for the user
                StartTonkGame();
                await Task.Delay(2500);
                susProcess = Process.GetProcessesByName("wotblitz").FirstOrDefault();
                pid = susProcess?.Id ?? 0;
                if (pid == 0) {
                    MessageBox.Show("Failed to start the wotblitz process.");
                    return;
                }
            }

            string worldOftonksExe = susProcess.MainModule.FileName;
            if (injectorSettings.LastExePath != worldOftonksExe) {
                injectorSettings.LastExePath = worldOftonksExe;
                SaveSettings();
            }
            
            string dllPath = txtDllPath.Text;
            if (!Injector.IsAlreadyInjected(pid, dllPath)) {
                if (Injector.InjectDll(pid, dllPath)) {
                    StatusText.Text = $"Status: Injected into {pid}!";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(128, 239, 128));
                } else {
                    MessageBox.Show($"Failed to inject into PID: {pid}");
                    StatusText.Text = $"Status: Injected into {pid}!";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                }
            } else {
                MessageBox.Show("Dll is already injected wtf dont do that...", "Warning", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            
            if (stillRunningTokenCancellationSource != null) {
                stillRunningTokenCancellationSource.Cancel();
                if (stillRunningThread != null && stillRunningThread.IsAlive) {
                    stillRunningThread.Join();
                }
            }
            
            stillRunningTokenCancellationSource = new CancellationTokenSource();
            stillRunningThread = new Thread(CheckIfStillRunning);
            stillRunningThread.Start();
        }

        private bool StartWithAppId() {
            string appId = "steam://rungameid/444200";
            Process.Start("explorer.exe", appId)?.WaitForExit();
            for (int i = 0; i < 100; i++) {
                Thread.Sleep(20);
                if (Process.GetProcessesByName("wotblitz").Length >= 1) {
                    return true;
                }
            }
            return false;
        }

        private bool TryStartWargaming() {
            string path = "C:\\Games\\World_of_Tanks_Blitz\\wotblitz.exe";
            if (!File.Exists(path)) {
                return false; // (not true) // (it very much means that the path doesn't exist (its false))
            }
            Process started = Process.Start(path);
            if (started == null) return false;
            return true;
        }

        private bool TryStartFromSettings() {
            if (string.IsNullOrEmpty(injectorSettings.LastExePath)) {
                return false;
            }

            if (!File.Exists(injectorSettings.LastExePath)) {
                return false;
            }
            Process started = Process.Start(injectorSettings.LastExePath);
            return started != null;
        }

        private bool StartTonkGame() {
            if (TryStartFromSettings()) {
                return true;
            }

            if (TryStartWargaming()) {
                return true;
            }

            if (StartWithAppId()) {
                return true;
            }

            return false;
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

    internal static class Injector {
        internal const uint PROCESS_ALL_ACCESS = 0x01F0FFF;
        internal const uint MEM_COMMIT = 0x1000;
        internal const uint MEM_RESERVE = 0x2000;
        internal const uint PAGE_READWRITE = 0x04;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(
            uint dwDesiredAccess,
            bool bInheritHandle,
            uint dwProcessId
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            uint flAllocationType,
            uint flProtect
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            uint nSize,
            out IntPtr lpNumberOfBytesWritten
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            IntPtr lpThreadAttributes,
            uint dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            out IntPtr lpThreadId
        );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        internal static bool InjectDll(int pid, string dllPath) {
            IntPtr hProc = OpenProcess(PROCESS_ALL_ACCESS, false, (uint)pid);
            if (hProc == IntPtr.Zero) {
                MessageBox.Show("Failed to open target process :(", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            byte[] dllBytes = System.Text.Encoding.Unicode.GetBytes(dllPath);
            IntPtr malloc = VirtualAllocEx(hProc, IntPtr.Zero, (uint)dllBytes.Length * 2 + 2, MEM_COMMIT | MEM_RESERVE,
                PAGE_READWRITE);
            if (malloc == IntPtr.Zero) {
                MessageBox.Show("Could not allocate memory to target process :(", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            WriteProcessMemory(hProc, malloc, dllBytes, (uint)dllBytes.Length, out _);

            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
            if (loadLibraryAddr == IntPtr.Zero) {
                MessageBox.Show("Could not get address of LoadLibraryW", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLibraryAddr,
                    malloc, 0, out _) == IntPtr.Zero) {
                MessageBox.Show("Could not create remote thread in target process :(", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return true;
        }

        internal static bool IsAlreadyInjected(int pid, string dllPath) {
            var process = Process.GetProcessById(pid);
            string fullDllPath = Path.GetFullPath(dllPath);
            if (process is null) return false;
            foreach (ProcessModule module in process.Modules) {
                if (Path.GetFullPath(module.FileName) == fullDllPath) {
                    return true;
                }
            }

            return false;
        }
    }
}


