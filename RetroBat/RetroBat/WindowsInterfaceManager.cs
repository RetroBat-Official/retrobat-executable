using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace RetroBat
{
    /// <summary>
    /// Reduces Windows interference while EmulationStation is running.
    /// Mode 1: notification toasts, accessibility shortcuts (Sticky / Filter / Toggle Keys) and
    /// the Xbox button opening the Game Bar are turned off.
    /// Mode 2: mode 1, plus the Windows taskbars and open folder windows are hidden and the
    /// crash dialogs of Windows Error Reporting are not shown.
    /// Mode 3 (arcade cabinet): mode 2, plus the Windows keys and exit shortcuts are blocked
    /// (see KioskKeyboardLock).
    /// Windows are hidden, not closed: killing explorer.exe makes Windows restart it a few seconds
    /// later (Winlogon AutoRestartShell), and closing it gracefully is not reliable across Windows
    /// versions. Hiding costs nothing, keeps every tray icon and every open folder alive, needs no
    /// admin rights, and behaves the same on every Windows version.
    /// The previous state is written to a sentinel file before anything is changed, so a session
    /// that ends badly (crash, process killed, power loss) is recovered at the next startup
    /// instead of leaving the user without a taskbar or with notifications disabled for good.
    /// </summary>
    internal static class WindowsInterfaceManager
    {
        private const string NotificationsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PushNotifications";
        private const string ToastValue = "ToastEnabled";
        private const string SentinelFileName = "winui.state";

        // Written in the sentinel file when ToastEnabled did not exist at all, which is the
        // factory state of Windows. Restoring it then means deleting the value, not writing 1.
        private const string AbsentMarker = "ABSENT";

        // Second line of the sentinel file, present only when this session hid the shell windows.
        private const string ShellUiMarker = "SHELLUI";

        // Next lines of the sentinel file: "KEY=previous value" for each setting changed by this session.
        private const string StickyKeysKey = "STICKYKEYS";
        private const string FilterKeysKey = "FILTERKEYS";
        private const string ToggleKeysKey = "TOGGLEKEYS";
        private const string GameBarKey = "GAMEBAR";
        private const string ErrorReportingKey = "WERUI";

        // Xbox button of the controller opening the Game Bar ("Open Xbox Game Bar using this button on a controller")
        private const string GameBarRegKey = @"SOFTWARE\Microsoft\GameBar";
        private const string GameBarValue = "UseNexusForGameBarEnabled";

        // "... has stopped working" dialogs of Windows Error Reporting (crashes are still logged)
        private const string ErrorReportingRegKey = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting";
        private const string ErrorReportingValue = "DontShowUI";

        // SystemParametersInfo: accessibility features and their keyboard shortcut flag
        private const uint SPI_GETFILTERKEYS = 0x0032;
        private const uint SPI_SETFILTERKEYS = 0x0033;
        private const uint SPI_GETTOGGLEKEYS = 0x0034;
        private const uint SPI_SETTOGGLEKEYS = 0x0035;
        private const uint SPI_GETSTICKYKEYS = 0x003A;
        private const uint SPI_SETSTICKYKEYS = 0x003B;
        private const uint FEATURE_ON = 0x00000001;     // SKF_STICKYKEYSON, FKF_FILTERKEYSON, TKF_TOGGLEKEYSON
        private const uint HOTKEY_ACTIVE = 0x00000004;  // SKF_HOTKEYACTIVE, FKF_HOTKEYACTIVE, TKF_HOTKEYACTIVE

        // Main taskbar, then one window of the secondary class per additional monitor.
        private const string PrimaryTaskbarClass = "Shell_TrayWnd";
        private const string SecondaryTaskbarClass = "Shell_SecondaryTrayWnd";

        // Open folder windows: current file explorer, and the legacy view still used by a few
        // shell entry points. The desktop (Progman) is deliberately left alone: hiding it leaves
        // the uncovered area unpainted instead of showing a clean background.
        private const string FileExplorerClass = "CabinetWClass";
        private const string LegacyExplorerClass = "ExploreWClass";

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // STICKYKEYS and TOGGLEKEYS share this layout (cbSize, dwFlags)
        [StructLayout(LayoutKind.Sequential)]
        private struct FLAGSPARAM
        {
            public uint cbSize;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILTERKEYS
        {
            public uint cbSize;
            public uint dwFlags;
            public uint iWaitMSec;
            public uint iDelayMSec;
            public uint iRepeatMSec;
            public uint iBounceMSec;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref FLAGSPARAM pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref FILTERKEYS pvParam, uint fWinIni);

        private static string _previousToast;
        private static bool _shellUiHidden;

        // Previous values of the other settings changed by this session (sentinel keys above)
        private static readonly Dictionary<string, string> _previousSettings = new Dictionary<string, string>();

        // Exact windows hidden by this session, so that restoring never touches a window the user
        // had hidden by other means.
        private static readonly List<IntPtr> _hiddenWindows = new List<IntPtr>();

        /// <summary>Applies the DisableWindowsInterface setting. Must be called once EmulationStation
        /// is up and focused: hiding the taskbar reshuffles the window z-order and would steal the
        /// focus that LaunchAndFocus just gave to the frontend.</summary>
        public static void Apply(string appFolder, int mode)
        {
            if (mode <= 0)
                return;

            string sentinel = GetSentinelPath(appFolder);

            // A leftover sentinel means the previous session never restored anything: its content
            // holds the real user value and must not be overwritten by the value we read now,
            // which is the one we ourselves forced last time.
            if (File.Exists(sentinel))
            {
                SimpleLogger.Instance.Warning("Previous RetroBat session did not restore the Windows interface, recovering its saved state.");

                bool shellUiWasHidden;
                ReadSentinel(sentinel, out _previousToast, out shellUiWasHidden, _previousSettings);

                // Put the windows back right away rather than at the end of this session: the user
                // may well have set the option back to 1, and would otherwise spend the whole
                // session without a taskbar.
                if (shellUiWasHidden)
                {
                    _shellUiHidden = true;
                    ShowShellUi();
                    _shellUiHidden = false;
                }
            }
            else
                _previousToast = ReadCurrentToastSetting();

            // Current values are read only when no recovered value exists: a recovered value is the
            // real user value, the current one is the value forced by the previous session.
            SaveCurrentSetting(StickyKeysKey, () => ReadFlags(SPI_GETSTICKYKEYS));
            SaveCurrentSetting(FilterKeysKey, () => ReadFilterKeysFlags());
            SaveCurrentSetting(ToggleKeysKey, () => ReadFlags(SPI_GETTOGGLEKEYS));
            SaveCurrentSetting(GameBarKey, () => ReadDword(GameBarRegKey, GameBarValue));

            if (mode >= 2)
                SaveCurrentSetting(ErrorReportingKey, () => ReadDword(ErrorReportingRegKey, ErrorReportingValue));

            WriteSentinel(sentinel, _previousToast, mode >= 2, _previousSettings);

            // Mode 2 includes mode 1 on purpose: with the taskbar hidden, notifications are still
            // displayed on top of the frontend, so they have to be turned off as well.
            DisableNotifications();
            DisableAccessibilityShortcuts();
            WriteDword(GameBarRegKey, GameBarValue, 0, "Game Bar controller button");

            if (mode >= 2)
            {
                HideShellUi();
                WriteDword(ErrorReportingRegKey, ErrorReportingValue, 1, "crash dialogs");
            }
        }

        /// <summary>Puts everything back. Safe to call even when Apply did nothing.</summary>
        public static void Restore(string appFolder)
        {
            string sentinel = GetSentinelPath(appFolder);

            if (_previousToast == null && File.Exists(sentinel))
                ReadSentinel(sentinel, out _previousToast, out _shellUiHidden, _previousSettings);

            if (_shellUiHidden)
                ShowShellUi();

            if (_previousToast != null)
                RestoreNotifications(_previousToast);

            RestoreSettings();

            try
            {
                if (File.Exists(sentinel))
                    File.Delete(sentinel);
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not delete state file: " + ex.Message); }

            _previousToast = null;
            _shellUiHidden = false;
            _hiddenWindows.Clear();
            _previousSettings.Clear();
        }

        private static string GetSentinelPath(string appFolder)
        {
            return Path.Combine(appFolder, SentinelFileName);
        }

        private static string ReadCurrentToastSetting()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(NotificationsKey))
                {
                    object value = key == null ? null : key.GetValue(ToastValue);
                    if (value != null)
                        return Convert.ToInt32(value).ToString();
                }
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not read current notification setting: " + ex.Message); }

            return AbsentMarker;
        }

        private static void WriteSentinel(string sentinel, string previousToast, bool shellUiHidden, Dictionary<string, string> previousSettings)
        {
            try
            {
                var lines = new List<string> { previousToast };

                if (shellUiHidden)
                    lines.Add(ShellUiMarker);

                foreach (var setting in previousSettings)
                    lines.Add(setting.Key + "=" + setting.Value);

                File.WriteAllLines(sentinel, lines.ToArray());
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not write state file: " + ex.Message); }
        }

        private static void ReadSentinel(string sentinel, out string previousToast, out bool shellUiHidden, Dictionary<string, string> previousSettings)
        {
            previousToast = AbsentMarker;
            shellUiHidden = false;
            previousSettings.Clear();

            try
            {
                string[] lines = File.ReadAllLines(sentinel);

                if (lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]))
                    previousToast = lines[0].Trim();

                // Older state files only have the toast line and the optional shell marker
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (string.Equals(line, ShellUiMarker, StringComparison.OrdinalIgnoreCase))
                        shellUiHidden = true;
                    else if (line.IndexOf('=') > 0)
                        previousSettings[line.Substring(0, line.IndexOf('='))] = line.Substring(line.IndexOf('=') + 1);
                }
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not read state file: " + ex.Message); }
        }

        /// <summary>Keeps the value recovered from a previous session if any, otherwise saves the current one.</summary>
        private static void SaveCurrentSetting(string key, Func<string> readCurrent)
        {
            if (_previousSettings.ContainsKey(key))
                return;

            string value = readCurrent();
            if (value != null)
                _previousSettings[key] = value;
        }

        private static void RestoreSettings()
        {
            string value;

            if (_previousSettings.TryGetValue(StickyKeysKey, out value))
                WriteFlags(SPI_GETSTICKYKEYS, SPI_SETSTICKYKEYS, value);

            if (_previousSettings.TryGetValue(FilterKeysKey, out value))
                WriteFilterKeysFlags(value);

            if (_previousSettings.TryGetValue(ToggleKeysKey, out value))
                WriteFlags(SPI_GETTOGGLEKEYS, SPI_SETTOGGLEKEYS, value);

            if (_previousSettings.TryGetValue(GameBarKey, out value))
                RestoreDword(GameBarRegKey, GameBarValue, value);

            if (_previousSettings.TryGetValue(ErrorReportingKey, out value))
                RestoreDword(ErrorReportingRegKey, ErrorReportingValue, value);

            if (_previousSettings.Count > 0)
                SimpleLogger.Instance.Info("Windows accessibility shortcuts, Game Bar button and crash dialogs settings restored.");
        }

        /// <summary>Turns off the keyboard shortcuts of Sticky Keys (Shift x5), Filter Keys (right Shift held)
        /// and Toggle Keys (Num Lock held). Features already turned on by the user are left alone.
        /// Not written to the user profile (no SPIF_UPDATEINIFILE): a log off also puts them back.</summary>
        private static void DisableAccessibilityShortcuts()
        {
            string value;

            if (_previousSettings.TryGetValue(StickyKeysKey, out value))
                WriteFlags(SPI_GETSTICKYKEYS, SPI_SETSTICKYKEYS, WithoutHotkey(value));

            if (_previousSettings.TryGetValue(FilterKeysKey, out value))
                WriteFilterKeysFlags(WithoutHotkey(value));

            if (_previousSettings.TryGetValue(ToggleKeysKey, out value))
                WriteFlags(SPI_GETTOGGLEKEYS, SPI_SETTOGGLEKEYS, WithoutHotkey(value));

            SimpleLogger.Instance.Info("Windows accessibility shortcuts disabled for this session.");
        }

        private static string WithoutHotkey(string flags)
        {
            uint value;
            if (!uint.TryParse(flags, out value) || (value & FEATURE_ON) != 0)
                return flags;

            return (value & ~HOTKEY_ACTIVE).ToString();
        }

        private static string ReadFlags(uint getAction)
        {
            var param = new FLAGSPARAM { cbSize = (uint)Marshal.SizeOf(typeof(FLAGSPARAM)) };
            if (SystemParametersInfo(getAction, param.cbSize, ref param, 0))
                return param.dwFlags.ToString();

            SimpleLogger.Instance.Warning("Could not read accessibility setting " + getAction + ".");
            return null;
        }

        private static void WriteFlags(uint getAction, uint setAction, string flags)
        {
            uint value;
            if (!uint.TryParse(flags, out value))
                return;

            var param = new FLAGSPARAM { cbSize = (uint)Marshal.SizeOf(typeof(FLAGSPARAM)) };
            if (!SystemParametersInfo(getAction, param.cbSize, ref param, 0) || param.dwFlags == value)
                return;

            param.dwFlags = value;
            if (!SystemParametersInfo(setAction, param.cbSize, ref param, 0))
                SimpleLogger.Instance.Warning("Could not change accessibility setting " + setAction + ".");
        }

        private static string ReadFilterKeysFlags()
        {
            var param = new FILTERKEYS { cbSize = (uint)Marshal.SizeOf(typeof(FILTERKEYS)) };
            if (SystemParametersInfo(SPI_GETFILTERKEYS, param.cbSize, ref param, 0))
                return param.dwFlags.ToString();

            SimpleLogger.Instance.Warning("Could not read Filter Keys setting.");
            return null;
        }

        private static void WriteFilterKeysFlags(string flags)
        {
            uint value;
            if (!uint.TryParse(flags, out value))
                return;

            var param = new FILTERKEYS { cbSize = (uint)Marshal.SizeOf(typeof(FILTERKEYS)) };
            if (!SystemParametersInfo(SPI_GETFILTERKEYS, param.cbSize, ref param, 0) || param.dwFlags == value)
                return;

            param.dwFlags = value;
            if (!SystemParametersInfo(SPI_SETFILTERKEYS, param.cbSize, ref param, 0))
                SimpleLogger.Instance.Warning("Could not change Filter Keys setting.");
        }

        private static string ReadDword(string subKey, string valueName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKey))
                {
                    object value = key == null ? null : key.GetValue(valueName);
                    if (value != null)
                        return Convert.ToInt32(value).ToString();
                }
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not read " + valueName + ": " + ex.Message); }

            return AbsentMarker;
        }

        private static void WriteDword(string subKey, string valueName, int value, string label)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey))
                    key.SetValue(valueName, value, RegistryValueKind.DWord);

                SimpleLogger.Instance.Info("Windows " + label + " disabled for this session.");
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not change " + valueName + ": " + ex.Message); }
        }

        /// <summary>Puts a registry value back, or deletes it when it did not exist before.</summary>
        private static void RestoreDword(string subKey, string valueName, string previous)
        {
            try
            {
                if (string.Equals(previous, AbsentMarker, StringComparison.OrdinalIgnoreCase))
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKey, true))
                    {
                        if (key != null)
                            key.DeleteValue(valueName, false);
                    }
                }
                else
                {
                    int value;
                    if (!int.TryParse(previous, out value))
                        return;

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey))
                        key.SetValue(valueName, value, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not restore " + valueName + ": " + ex.Message); }
        }

        private static void DisableNotifications()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(NotificationsKey))
                    key.SetValue(ToastValue, 0, RegistryValueKind.DWord);

                SimpleLogger.Instance.Info("Windows notifications disabled for this session.");
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not disable notifications: " + ex.Message); }
        }

        private static void RestoreNotifications(string previousToast)
        {
            try
            {
                if (string.Equals(previousToast, AbsentMarker, StringComparison.OrdinalIgnoreCase))
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(NotificationsKey, true))
                    {
                        if (key != null)
                            key.DeleteValue(ToastValue, false);
                    }
                }
                else
                {
                    int value;
                    if (!int.TryParse(previousToast, out value))
                        value = 1;

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(NotificationsKey))
                        key.SetValue(ToastValue, value, RegistryValueKind.DWord);
                }

                SimpleLogger.Instance.Info("Windows notifications restored.");
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not restore notifications: " + ex.Message); }
        }

        /// <summary>Collects the main taskbar plus one window per secondary monitor. Hidden windows
        /// are still returned by FindWindowEx, so this also works when restoring.</summary>
        private static List<IntPtr> GetTaskbarWindows()
        {
            var windows = new List<IntPtr>();

            IntPtr primary = FindWindowEx(IntPtr.Zero, IntPtr.Zero, PrimaryTaskbarClass, null);
            if (primary != IntPtr.Zero)
                windows.Add(primary);

            IntPtr secondary = IntPtr.Zero;
            while ((secondary = FindWindowEx(IntPtr.Zero, secondary, SecondaryTaskbarClass, null)) != IntPtr.Zero)
                windows.Add(secondary);

            return windows;
        }

        /// <summary>Collects the open folder windows. Their number varies during the session, so
        /// they are enumerated rather than looked up: pass true to find the ones to hide, false to
        /// find the ones left hidden by a session that never restored them.</summary>
        private static List<IntPtr> GetFolderWindows(bool visible)
        {
            var windows = new List<IntPtr>();
            var className = new StringBuilder(256);

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    className.Length = 0;

                    if (GetClassName(hWnd, className, className.Capacity) == 0)
                        return true;

                    string name = className.ToString();
                    if (name != FileExplorerClass && name != LegacyExplorerClass)
                        return true;

                    if (IsWindowVisible(hWnd) == visible)
                        windows.Add(hWnd);

                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not enumerate folder windows: " + ex.Message); }

            return windows;
        }

        private static void HideShellUi()
        {
            try
            {
                // Flagged before anything is hidden: from now on this session owes a restore,
                // whatever happens next.
                _shellUiHidden = true;
                _hiddenWindows.Clear();

                int taskbars = HideWindows(GetTaskbarWindows());
                int folders = HideWindows(GetFolderWindows(true));

                SimpleLogger.Instance.Info("Windows interface hidden for this session: " + taskbars + " taskbar window(s), " + folders + " folder window(s).");
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not hide the Windows interface: " + ex.Message); }
        }

        private static int HideWindows(List<IntPtr> windows)
        {
            int hidden = 0;

            foreach (IntPtr window in windows)
            {
                ShowWindow(window, SW_HIDE);

                if (IsWindowVisible(window))
                {
                    SimpleLogger.Instance.Warning("A window refused to hide, leaving it alone.");
                    continue;
                }

                _hiddenWindows.Add(window);
                hidden++;
            }

            return hidden;
        }

        private static void ShowShellUi()
        {
            try
            {
                var windows = new List<IntPtr>(_hiddenWindows);

                // Recovery path: a previous session crashed and left windows hidden, so nothing was
                // recorded. Find the taskbars by class, and the folder windows that are still hidden.
                if (windows.Count == 0)
                {
                    windows.AddRange(GetTaskbarWindows());
                    windows.AddRange(GetFolderWindows(false));
                }

                int shown = 0;
                foreach (IntPtr window in windows)
                {
                    ShowWindow(window, SW_SHOW);
                    if (IsWindowVisible(window))
                        shown++;
                }

                _hiddenWindows.Clear();

                if (shown == windows.Count)
                    SimpleLogger.Instance.Info("Windows interface restored (" + shown + " window(s)).");
                else
                    SimpleLogger.Instance.Warning("Only " + shown + " of " + windows.Count + " window(s) could be restored. A window closed during the session no longer needs to be.");
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not restore the Windows interface: " + ex.Message); }
        }
    }
}