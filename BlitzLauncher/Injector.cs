using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlitzLauncher {
    internal static class Injector {
        internal const uint PROCESS_ALL_ACCESS = 0x01F0FFF;
        internal const uint MEM_COMMIT = 0x1000;
        internal const uint MEM_RESERVE = 0x2000;
        internal const uint PAGE_READWRITE = 0x04;
        
        const uint WAIT_ABANDONED = 0x00000080;
        const uint WAIT_OBJECT_0 = 0x00000000;
        const uint WAIT_TIMEOUT = 0x00000102;
        const uint WAIT_FAILED = 0xFFFFFFFF;
        
        const int THREAD_PRIORITY_LOWEST = -2;
        const int THREAD_PRIORITY_BELOW_NORMAL = -1;
        const int THREAD_PRIORITY_NORMAL = 0;
        const int THREAD_PRIORITY_ABOVE_NORMAL = 1;
        const int THREAD_PRIORITY_HIGHEST = 2;
        const int THREAD_PRIORITY_TIME_CRITICAL = 15;
        const int THREAD_PRIORITY_IDLE = -15;

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetThreadPriority(IntPtr hThread, int nPriority);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        internal static bool InjectDll(ProcessManager.SuspendedProcess process, string dllPath) {
            IntPtr hProc = process.ProcessHandle;

            byte[] dllBytes = System.Text.Encoding.Unicode.GetBytes(dllPath);
            IntPtr remoteMemory = VirtualAllocEx(hProc, IntPtr.Zero, (uint)dllBytes.Length * 2 + 2, MEM_COMMIT | MEM_RESERVE,
                PAGE_READWRITE);
            if (remoteMemory == IntPtr.Zero) {
                MessageBox.Show("Could not allocate memory to target process :(", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            WriteProcessMemory(hProc, remoteMemory, dllBytes, (uint)dllBytes.Length, out _);

            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
            if (loadLibraryAddr == IntPtr.Zero) {
                MessageBox.Show("Could not get address of LoadLibraryW", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            IntPtr remoteThreadHandle = CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLibraryAddr, remoteMemory, 0, out _);
            if (remoteThreadHandle == IntPtr.Zero) {
                MessageBox.Show("Could not create remote thread in target process :(", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            } else {
                SetThreadPriority(remoteThreadHandle, THREAD_PRIORITY_TIME_CRITICAL);
                WaitForSingleObject(remoteThreadHandle, 5000);
                if (WaitForSingleObject(remoteThreadHandle, 2000) == WAIT_TIMEOUT) {
                    process.Resume(); // injected too early, resuming it so that our dll injects.
                    if (WaitForSingleObject(remoteThreadHandle, 5000) == WAIT_TIMEOUT) {
                        MessageBox.Show("I guess it couldn't inject :( (timed out)", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                CloseHandle(remoteThreadHandle);
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
