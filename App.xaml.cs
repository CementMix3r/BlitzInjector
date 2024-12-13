using System.Runtime.InteropServices;
using System.Windows;

namespace BlitzLauncher {
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
            DpiHelper.SetProcessDpiAwareness(DpiHelper.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE);
            base.OnStartup(e);
        }
    }
    internal static class DpiHelper {
        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(int dpiAwareness);
        public const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = 2;
    }
}
