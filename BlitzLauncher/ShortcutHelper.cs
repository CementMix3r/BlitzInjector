using System;
using System.IO;
using System.Runtime.InteropServices;
using Shell32;


namespace BlitzLauncher {
    internal static class ShortcutHelper {
        internal static string GetShortcutTarget(string shortcutPath) {
            if (!File.Exists(shortcutPath)) {
                throw new FileNotFoundException($"Shortcut not found: {nameof(shortcutPath)}");
            }

            Shell shell = new Shell();
            Folder folder = shell.NameSpace(Path.GetDirectoryName(shortcutPath));
            FolderItem item = folder.ParseName(Path.GetFileName(shortcutPath));
            if (item != null && item.IsLink) {
                ShellLinkObject lnk = (ShellLinkObject)item.GetLink;
                return lnk.Target.Path;
            }

            return null;
        }
    }
}