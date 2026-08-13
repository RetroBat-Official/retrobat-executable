using System;
using System.IO;
using Microsoft.Win32;

namespace RetroBat
{
    internal static class AutostartManager
    {
        public static void CleanupLegacyShortcut()
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string linkStartup = Path.Combine(startupFolder, "RetroBat.lnk");

                if (File.Exists(linkStartup))
                {
                    try { File.Delete(linkStartup); }
                    catch (Exception ex) { SimpleLogger.Instance.Warning("Failed to delete legacy RetroBat.lnk: " + ex.Message); }
                }
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("CleanupStartup failed: " + ex.Message); }
        }

        public static void Apply(int autostartMode, string appFolder, string appExe)
        {
            if (autostartMode == 1)
            {
                AddToStartupFolder(appFolder, appExe);
                RemoveFromStartupReg();
            }
            else if (autostartMode == 2)
            {
                AddToStartupReg(appFolder, appExe);
                RemoveFromStartupFolder("RetroBat");
            }
            else
            {
                RemoveFromStartupReg();
                RemoveFromStartupFolder("RetroBat");
            }
        }

        private static void AddToStartupReg(string appPath, string appExe)
        {
            SimpleLogger.Instance.Info("Setting RetroBat to launch at startup.");

            string batPath = Path.Combine(appPath, appExe);

            string regValue = string.Format(
            "cmd.exe /c \"cd /d {0} && start \"\" \"{1}\"\"\"",
            appPath,
            batPath
        );

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key.SetValue("RetroBat", regValue);
                SimpleLogger.Instance.Info("RetroBat set in registry to startup.");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to set startup registry key: " + ex.Message);
            }
        }

        private static void AddToStartupFolder(string exePath, string shortcutName)
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string exeName = Path.GetFileNameWithoutExtension(shortcutName);
                string batPath = Path.Combine(startupFolder, exeName + ".bat");
                string exe = Path.Combine(exePath, shortcutName);

                // Write a simple batch file to start RetroBat
                string batContent = $"@echo off{Environment.NewLine}cd /d \"{exePath}\"{Environment.NewLine}\"{exe}\"";
                File.WriteAllText(batPath, batContent);

                SimpleLogger.Instance.Info("RetroBat batch added to Startup folder: " + batPath);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to add RetroBat to Startup folder: " + ex.Message);
            }
        }

        private static void RemoveFromStartupFolder(string shortcutName)
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string batPath = Path.Combine(startupFolder, shortcutName + ".bat");

                if (File.Exists(batPath))
                {
                    File.Delete(batPath);
                    SimpleLogger.Instance.Info("RetroBat removed from Startup folder: " + batPath);
                }
                else
                {
                    SimpleLogger.Instance.Info("RetroBat startup batch not found, nothing to remove.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to remove RetroBat from Startup folder: " + ex.Message);
            }
        }

        private static void RemoveFromStartupReg()
        {
            SimpleLogger.Instance.Info("Ensuring RetroBat does not launch at startup.");

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key.DeleteValue("RetroBat");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to remove startup registry key: " + ex.Message);
            }
        }
    }
}
