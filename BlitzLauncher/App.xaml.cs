using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Globalization;

namespace BlitzLauncher {
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);
            DpiHelper.SetProcessDpiAwareness(DpiHelper.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
        }
    }
    internal static class DpiHelper {
        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(int dpiAwareness);
        public const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = 2;
    }
}
