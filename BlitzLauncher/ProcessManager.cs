﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;


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

        private static SuspendedProcess TryStartWargaming(Settings settings) {
            string path = "C:\\Games\\World_of_Tanks_Blitz\\wotblitz.exe";
            if (!File.Exists(path)) {
                return new SuspendedProcess(); // (not true) // (it very much means that the path doesn't exist (its false))
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
