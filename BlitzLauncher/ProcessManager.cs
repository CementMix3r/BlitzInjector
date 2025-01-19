using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml;


namespace BlitzLauncher {
    internal class ProcessManager {
        [StructLayout(LayoutKind.Sequential)]
        internal struct STARTUPINFO {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
        public const uint SW_HIDE = 0;
        public const uint SW_SHOW = 5;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;
        private const uint SC_MINIMIZE = 0xF020;
        private const uint WM_SYSCOMMAND = 0x0112;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);
        private const uint CREATE_SUSPENDED = 0x00000004;
        private const uint DETACHED_PROCESS = 0x00000008;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        private static readonly uint THREAD_SUSPEND_RESUME = 2;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(
            uint dwDesiredAccess,
            bool bInheritHandle,
            uint dwProcessId
        );
        internal const uint PROCESS_ALL_ACCESS = 0x01F0FFF;

        internal class SuspendedProcess : IDisposable {
            public IntPtr ThreadHandle = IntPtr.Zero;
            public List<IntPtr> ThreadHandleList = new List<IntPtr>();
            public IntPtr ProcessHandle = IntPtr.Zero;
            public uint ProcessId = 0;
            public bool IsOpen => ProcessId != 0;

            public void Resume() {
                foreach (IntPtr handle in ThreadHandleList) {
                    if (handle == IntPtr.Zero) continue;
                    try {
                        int res = ResumeThread(handle);
                        if (res == -1) {
                            MessageBox.Show(
                                $"Failed to resume thread: {Marshal.GetLastWin32Error()}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    } catch {
                        // ignore
                    }
                }

                if (ThreadHandle == IntPtr.Zero) {
                    return;
                }
                try {
                    int res = ResumeThread(ThreadHandle);
                    if (res == -1) {
                        MessageBox.Show(
                            $"Failed to resume thread: {Marshal.GetLastWin32Error()}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                } catch {
                    // ignore
                }
            }
            public void Suspend() {
                foreach (IntPtr handle in ThreadHandleList) {
                    if (handle == IntPtr.Zero) continue;
                    try {
                        int res = SuspendThread(handle);
                        if (res == -1) {
                            MessageBox.Show(
                                $"Failed to suspend thread: {Marshal.GetLastWin32Error()}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    } catch {
                        // ignore
                    }
                }

                if (ThreadHandle == IntPtr.Zero) {
                    return;
                }
                try {
                    int res = SuspendThread(ThreadHandle);
                    if (res == -1) {
                        MessageBox.Show(
                            $"Failed to suspend thread: {Marshal.GetLastWin32Error()}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                } catch {
                    // ignore
                }
            }

            public void Dispose() {
                if (ThreadHandle != IntPtr.Zero) {
                    if (!CloseHandle(ThreadHandle)) {
                        MessageBox.Show($"Failed to close thread handle: {Marshal.GetLastWin32Error()}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    ThreadHandle = IntPtr.Zero;
                }

                foreach (IntPtr hThread in ThreadHandleList) {
                    if (hThread != IntPtr.Zero) {
                        if (!CloseHandle(hThread)) {
                            MessageBox.Show($"Failed to close thread handle: {Marshal.GetLastWin32Error()}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                if (ProcessHandle != IntPtr.Zero) {
                    if (!CloseHandle(ProcessHandle)) {
                        MessageBox.Show($"Failed to close process handle: {Marshal.GetLastWin32Error()}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    ProcessHandle = IntPtr.Zero;
                }
            }
        }


        internal static SuspendedProcess StartInSuspendedState(string path) {
            var startupInfo = new STARTUPINFO();
            var processInfo = new PROCESS_INFORMATION();
            bool opened = CreateProcess(
                path,
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CREATE_SUSPENDED | DETACHED_PROCESS,
                IntPtr.Zero,
                null,
                ref startupInfo,
                out processInfo
            );
            if (!opened) {
                MessageBox.Show($"Failed to start process: {Marshal.GetLastWin32Error()}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return new SuspendedProcess();
            }
            return new SuspendedProcess {
                ThreadHandle = processInfo.hThread,
                ProcessHandle = processInfo.hProcess,
                ProcessId = processInfo.dwProcessId
            };
        }

        private static SuspendedProcess TryStartWithAppId(Settings settings) {
            string appId = "steam://rungameid/444200";
            Process.Start("explorer.exe", appId);
            return BusyWaitSuspendProcess();
        }

        private static string GetWGCPathFromShortcut() {
            try {
                string smPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs",
                    "Wargaming.net", "Wargaming.net Game Center.lnk");

                if (!File.Exists(smPath)) return GetDefaultWGCExePath();
                string target = ShortcutHelper.GetShortcutTarget(smPath);
                if (!string.IsNullOrEmpty(smPath)) {
                    return target;
                }
            } catch (Exception ex) {
                MessageBox.Show($"Could not locate WGC executable: {ex.Message} - {Marshal.GetLastWin32Error()}",
                    "WGC Path Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return GetDefaultWGCExePath();
        }

        private static string GetWGCLaunchPath() {
            string target = GetWGCPathFromShortcut(); 
            DirectoryInfo parentPath = Directory.GetParent(target);
            string xmlPath = Path.Combine(parentPath.FullName, "preferences.xml");
            if (string.IsNullOrEmpty(xmlPath)) {
                MessageBox.Show($"The XML file: {xmlPath} wasnt found... Trying shortcut resolver..", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);

                throw new FileNotFoundException($"Could not find file: {xmlPath}");
            }

            XmlDocument xml = new XmlDocument();
            xml.Load(xmlPath);

            XmlNodeList gNodes = xml.SelectNodes("//games_manager/games/game");
            if (gNodes == null || gNodes.Count == 0) {
                MessageBox.Show("No game nodes found :(", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw new InvalidOperationException("No game nodes found");
            }

            foreach (XmlNode gNode in gNodes) {
                XmlNode path = gNode.SelectSingleNode("working_dir");
                if (path != null && path.InnerText.Contains("World_of_Tanks_Blitz")) {
                    return Path.Combine(path.InnerText.Trim(), "wotblitz.exe");
                }
            }

            return "C:\\Games\\World_of_Tanks_Blitz\\wotblitz.exe";
        }

        private static string GetDefaultWGCExePath() {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Wargaming.net", "GameCenter", "wgc.exe");
            return path;
        }

        private static SuspendedProcess TryStartWargaming(Settings settings) {
            string path = GetWGCLaunchPath();
            string wgcPath = GetWGCPathFromShortcut();
            bool wgcIsRunning = Process.GetProcessesByName("wgc").Length > 0;
            if (wgcPath == null) {
                MessageBox.Show($"Somehow the WGC app has not been found.. Error: {Marshal.GetLastWin32Error()}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (!wgcIsRunning) {
                Process.Start(wgcPath);
                IntPtr hWnd = IntPtr.Zero;
                Stopwatch timeout = Stopwatch.StartNew();
                while (timeout.Elapsed.Seconds < 3) {
                    hWnd = FindWindow(null, "Wargaming.net Game Center");
                    if (hWnd != IntPtr.Zero) {
                        break;
                    }
                    Thread.Sleep(5);
                }

                if (hWnd != IntPtr.Zero) {
                    Thread.Sleep(2500);
                    ShowWindow(hWnd, SW_HIDE);
                }
            }

            if (!File.Exists(path)) {
                return new SuspendedProcess();
            }
            settings.LastExePath = path;
            return TryStartFromLastExe(settings);
        }

        private static SuspendedProcess TryStartUWP() {
            Process.Start("powershell.exe",
                "-Command \"Get-AppxPackage | Where-Object {$_.Name -like '*WorldofTanksBlitz'} | Foreach { Start-Process ('shell:appsfolder\\' + $_.PackageFamilyName + '!App') }\"\n");
            return BusyWaitSuspendProcess();
        }

        private static SuspendedProcess TryStartFromLastExe(Settings settings) {
            if (string.IsNullOrEmpty(settings.LastExePath)) {
                return new SuspendedProcess();
            }

            if (!File.Exists(settings.LastExePath)) {
                return new SuspendedProcess();
            }

            return StartInSuspendedState(settings.LastExePath);
        }
        private static SuspendedProcess TryStartFromCustomExe(Settings settings) {
            if (string.IsNullOrEmpty(settings.CustomExePath)) {
                return new SuspendedProcess();
            }

            if (!File.Exists(settings.CustomExePath)) {
                return new SuspendedProcess();
            }

            return StartInSuspendedState(settings.CustomExePath);
        }

        public static SuspendedProcess BusyWaitSuspendProcess() {
            Stopwatch timeout = Stopwatch.StartNew();
            while (timeout.Elapsed.Seconds < 6) {
                var processes = Process.GetProcessesByName("wotblitz");
                if (processes.Length >= 1) {
                    var result = new SuspendedProcess();
                    var proc = processes[0];
                    result.ProcessId = (uint)proc.Id;
                    foreach (ProcessThread thread in proc.Threads) {
                        IntPtr hThread = OpenThread(THREAD_SUSPEND_RESUME, false, (uint)thread.Id);
                        SuspendThread(hThread);
                        result.ThreadHandleList.Add(hThread);
                    }

                    IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, result.ProcessId);
                    result.ProcessHandle = hProcess;
                    return result;
                }
            }
            return new SuspendedProcess();
        }

        public static SuspendedProcess StartTonkGame(Settings settings) {
            SuspendedProcess opened = new SuspendedProcess();
            switch (settings.LaunchMode) {
                case LaunchMode.Automatic: {
                        opened = TryStartFromLastExe(settings);
                        if (opened.IsOpen) return opened;

                        opened = TryStartFromCustomExe(settings);
                        if (opened.IsOpen) return opened;

                        opened = TryStartWargaming(settings);
                        if (opened.IsOpen) return opened;

                        opened = TryStartWithAppId(settings);
                        if (opened.IsOpen) return opened;

                        opened = TryStartUWP();
                        if (opened.IsOpen) return opened;

                        return opened;
                    }
                case LaunchMode.CustomPath:
                    return TryStartFromCustomExe(settings);
                case LaunchMode.ForceSteam:
                    return TryStartWithAppId(settings);
                case LaunchMode.ForceWGC:
                    return TryStartWargaming(settings);
                case LaunchMode.ForceUWP:
                    return TryStartUWP();
            }

            return opened;
        }

    }
}
