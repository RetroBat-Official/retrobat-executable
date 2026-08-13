using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace RetroBat
{
    internal static class DpiAwarenessManager
    {
        public static void ApplyOverridesIfNeeded(string appFolder)
        {
            if (!HasDpiScaling())
                return;

            string dpiFile = Path.Combine(appFolder, "system", "tools", "dpi_awareness.txt");

            if (!File.Exists(dpiFile))
                return;

            try
            {
                var dpiLines = File.ReadAllLines(dpiFile);

                if (dpiLines.Length > 0)
                {
                    foreach (var dpiLine in dpiLines)
                    {
                        string dpiExePath = Path.Combine(appFolder, dpiLine.Trim());

                        if (File.Exists(dpiExePath))
                            SetDpiAwarenessOverride(dpiExePath, true);
                    }
                }
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Failed to apply DPI awareness overrides: " + ex.Message); }
        }

        public static bool HasDpiScaling()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontDPI"))
            {
                object val = key != null ? key.GetValue("LogPixels") : null;
                if (val is int dpi)
                    return dpi != 96;
            }

            using (var key = Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Desktop"))
            {
                object val = key != null ? key.GetValue("LogPixels") : null;
                if (val is int dpi)
                    return dpi != 96;
            }

            return false;
        }

        public static void SetDpiAwarenessOverride(string exePath, bool enable)
        {
            const string keyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

            RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, true)
                           ?? Registry.CurrentUser.CreateSubKey(keyPath);

            if (key == null)
                return;

            using (key)
            {
                string current = key.GetValue(exePath) as string ?? string.Empty;

                var flags = new HashSet<string>(current.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                if (enable)
                {
                    if (flags.Contains("HIGHDPIAWARE"))
                        return;
                    flags.Add("HIGHDPIAWARE");
                }
                else
                {
                    if (!flags.Contains("HIGHDPIAWARE"))
                        return;
                    flags.Remove("HIGHDPIAWARE");
                }

                if (flags.Count == 0)
                    key.DeleteValue(exePath, false);
                else
                    key.SetValue(exePath, string.Join(" ", flags), RegistryValueKind.String);
            }
        }
    }
}
