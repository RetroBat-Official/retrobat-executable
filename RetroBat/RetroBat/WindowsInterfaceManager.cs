using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace RetroBat
{
    /// <summary>
    /// Reduces Windows interference while EmulationStation is running: hides notification
    /// toasts (mode 1) and also hides the Windows taskbars and any open folder window (mode 2).
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

        private static string _previousToast;
        private static bool _shellUiHidden;

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
                ReadSentinel(sentinel, out _previousToast, out shellUiWasHidden);

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

            WriteSentinel(sentinel, _previousToast, mode >= 2);

            // Mode 2 includes mode 1 on purpose: with the taskbar hidden, notifications are still
            // displayed on top of the frontend, so they have to be turned off as well.
            DisableNotifications();

            if (mode >= 2)
                HideShellUi();
        }

        /// <summary>Puts everything back. Safe to call even when Apply did nothing.</summary>
        public static void Restore(string appFolder)
        {
            string sentinel = GetSentinelPath(appFolder);

            if (_previousToast == null && File.Exists(sentinel))
                ReadSentinel(sentinel, out _previousToast, out _shellUiHidden);

            if (_shellUiHidden)
                ShowShellUi();

            if (_previousToast != null)
                RestoreNotifications(_previousToast);

            try
            {
                if (File.Exists(sentinel))
                    File.Delete(sentinel);
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not delete state file: " + ex.Message); }

            _previousToast = null;
            _shellUiHidden = false;
            _hiddenWindows.Clear();
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

        private static void WriteSentinel(string sentinel, string previousToast, bool shellUiHidden)
        {
            try
            {
                File.WriteAllLines(sentinel, shellUiHidden
                    ? new[] { previousToast, ShellUiMarker }
                    : new[] { previousToast });
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not write state file: " + ex.Message); }
        }

        private static void ReadSentinel(string sentinel, out string previousToast, out bool shellUiHidden)
        {
            previousToast = AbsentMarker;
            shellUiHidden = false;

            try
            {
                string[] lines = File.ReadAllLines(sentinel);

                if (lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]))
                    previousToast = lines[0].Trim();

                shellUiHidden = lines.Length > 1 && string.Equals(lines[1].Trim(), ShellUiMarker, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not read state file: " + ex.Message); }
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