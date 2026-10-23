using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace Permadelete.Helpers
{
    public static class ContextMenuHelper
    {
        private const string FILE_SHELL_KEY = @"Software\Classes\*\shell\Permadelete";
        private const string DIRECTORY_SHELL_KEY = @"Software\Classes\Directory\shell\Permadelete";
        private const string MENU_TITLE = "Shred with Permadelete";

        /// <summary>
        /// Gets the executable path for Permadelete.exe.
        /// </summary>
        public static string GetExecutablePath()
        {
            try
            {
                string entryLocation = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(entryLocation) &&
                    Path.GetFileName(entryLocation).Equals("Permadelete.exe", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(entryLocation))
                {
                    return entryLocation;
                }

                string currentAssembly = typeof(ContextMenuHelper).Assembly.Location;
                if (!string.IsNullOrEmpty(currentAssembly))
                {
                    string dir = Path.GetDirectoryName(currentAssembly);
                    string target = Path.Combine(dir, "Permadelete.exe");
                    if (File.Exists(target))
                        return target;
                }

                string baseDirExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Permadelete.exe");
                if (File.Exists(baseDirExe))
                {
                    return baseDirExe;
                }

                var process = Process.GetCurrentProcess();
                string mainModule = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(mainModule) &&
                    Path.GetFileName(mainModule).Equals("Permadelete.exe", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(mainModule))
                {
                    return mainModule;
                }

                return baseDirExe;
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Permadelete.exe");
            }
        }

        /// <summary>
        /// Checks if the right-click context menu item is registered and points to the current executable.
        /// </summary>
        public static bool IsRegistered()
        {
            try
            {
                string currentExe = GetExecutablePath();
                if (string.IsNullOrEmpty(currentExe))
                    return false;

                string expectedCommand = $"\"{currentExe}\" \"%1\"";

                using (var fileKey = Registry.CurrentUser.OpenSubKey(FILE_SHELL_KEY + @"\command"))
                {
                    if (fileKey == null)
                        return false;

                    var cmdVal = fileKey.GetValue("") as string;
                    if (!string.Equals(cmdVal, expectedCommand, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                using (var dirKey = Registry.CurrentUser.OpenSubKey(DIRECTORY_SHELL_KEY + @"\command"))
                {
                    if (dirKey == null)
                        return false;

                    var cmdVal = dirKey.GetValue("") as string;
                    if (!string.Equals(cmdVal, expectedCommand, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Registers the context menu item for both files and directories in HKCU.
        /// Does not require administrative elevation.
        /// </summary>
        public static bool Register()
        {
            try
            {
                string exePath = GetExecutablePath();
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return false;

                string command = $"\"{exePath}\" \"%1\"";
                string icon = $"\"{exePath}\",0";

                // 1. Register for all files (*)
                using (var key = Registry.CurrentUser.CreateSubKey(FILE_SHELL_KEY))
                {
                    if (key != null)
                    {
                        key.SetValue("", MENU_TITLE);
                        key.SetValue("Icon", icon);

                        using (var cmdKey = key.CreateSubKey("command"))
                        {
                            cmdKey?.SetValue("", command);
                        }
                    }
                }

                // 2. Register for directories
                using (var key = Registry.CurrentUser.CreateSubKey(DIRECTORY_SHELL_KEY))
                {
                    if (key != null)
                    {
                        key.SetValue("", MENU_TITLE);
                        key.SetValue("Icon", icon);

                        using (var cmdKey = key.CreateSubKey("command"))
                        {
                            cmdKey?.SetValue("", command);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Services.LoggerService.Instance.Warning(ex);
                return false;
            }
        }

        /// <summary>
        /// Unregisters the context menu item for both files and directories.
        /// </summary>
        public static bool Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(FILE_SHELL_KEY, false);
                Registry.CurrentUser.DeleteSubKeyTree(DIRECTORY_SHELL_KEY, false);
                return true;
            }
            catch (Exception ex)
            {
                Services.LoggerService.Instance.Warning(ex);
                return false;
            }
        }

        /// <summary>
        /// Synchronizes the registry state with the user's preference.
        /// </summary>
        public static void Sync(bool enable)
        {
            if (enable)
            {
                Register();
            }
            else
            {
                Unregister();
            }
        }
    }
}
