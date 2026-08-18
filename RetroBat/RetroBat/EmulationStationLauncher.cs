using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RetroBat
{
    internal static class EmulationStationLauncher
    {
        public static string BuildArguments(RetroBatConfig config, string esPath, Screen[] screens)
        {
            List<string> commandArray = new List<string>();

            bool borderless = config.FullscreenBorderless;

            if (config.Fullscreen && config.ForceFullscreenRes)
            {
                commandArray.Add("--resolution");
                commandArray.Add(config.WindowXSize.ToString());
                commandArray.Add(config.WindowYSize.ToString());
            }
            else if (!config.Fullscreen && !borderless)
            {
                commandArray.Add("--windowed");
                commandArray.Add("--resolution");
                commandArray.Add(config.WindowXSize.ToString());
                commandArray.Add(config.WindowYSize.ToString());
            }
            else if (borderless)
            {
                commandArray.Add("--fullscreen-borderless");
            }
            else
            {
                commandArray.Add("--fullscreen");
            }

            if (config.GameListOnly)
                commandArray.Add("--gamelist-only");

            if (config.InterfaceMode == 2)
                commandArray.Add("--force-kid");
            else if (config.InterfaceMode == 1)
                commandArray.Add("--force-kiosk");

            if (config.MonitorIndex > 0 && config.MonitorIndex < screens.Length)
            {
                commandArray.Add("--monitor");
                commandArray.Add(config.MonitorIndex.ToString());
            }

            if (config.NoExitMenu)
                commandArray.Add("--no-exit");

            if (config.VSync)
                commandArray.Add("--vsync 1");
            else
                commandArray.Add("--vsync 0");

            if (config.DrawFramerate)
                commandArray.Add("--draw-framerate");

            commandArray.Add("--home");
            commandArray.Add(esPath);

            return string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a));
        }

        public static void RunWiimoteGun(string esPath)
        {
            SimpleLogger.Instance.Info("Running WiimoteGun.");

            string wgunExe = Path.Combine(esPath, "WiimoteGun.exe");

            if (!File.Exists(wgunExe))
            {
                SimpleLogger.Instance.Warning("WiimoteGun executable not found at: " + wgunExe);
                return;
            }

            try
            {
                var wgStart = new ProcessStartInfo
                {
                    FileName = wgunExe,
                    WorkingDirectory = esPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(wgStart);
                SimpleLogger.Instance.Info("WiimoteGun started successfully.");
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Failed to start WiimoteGun: " + ex.Message); }
        }

        /// <summary>Launches every configured companion app (AppLauncher, AppLauncher2... in retrobat.ini), if any, in parallel with EmulationStation. Fire-and-forget: does not wait for them to exit.</summary>
        public static void RunExternalApps(IEnumerable<string> appLaunchers)
        {
            if (appLaunchers == null)
                return;

            foreach (var appLauncher in appLaunchers)
                RunExternalApp(appLauncher);
        }

        /// <summary>Launches a single companion app entry. Append " -nowindow" to the ini value to start it hidden; otherwise it starts normally.</summary>
        private static void RunExternalApp(string appLauncher)
        {
            if (string.IsNullOrWhiteSpace(appLauncher))
                return;

            string appPath = ParseAppLauncherPath(appLauncher, out bool noWindow);

            if (string.IsNullOrWhiteSpace(appPath) || !File.Exists(appPath))
            {
                SimpleLogger.Instance.Warning("AppLauncher file not found at: " + appPath);
                return;
            }

            SimpleLogger.Instance.Info("Starting external app: " + appPath + (noWindow ? " (no window)" : ""));

            try
            {
                var appStart = new ProcessStartInfo
                {
                    FileName = appPath,
                    WorkingDirectory = Path.GetDirectoryName(appPath),
                    UseShellExecute = !noWindow,
                    CreateNoWindow = noWindow
                };

                Process.Start(appStart);
                SimpleLogger.Instance.Info("External app started successfully.");
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Failed to start external app: " + ex.Message); }
        }

        private static string ParseAppLauncherPath(string raw, out bool noWindow)
        {
            noWindow = false;
            string value = raw.Trim();

            const string flag = "-nowindow";
            if (value.EndsWith(flag, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - flag.Length).Trim();
                noWindow = true;
            }

            return value.Trim('"');
        }

        /// <summary>Starts EmulationStation and waits for/restores focus on its window. Returns false if the process failed to start (exe == null or an exception was thrown).</summary>
        public static bool LaunchAndFocus(ProcessStartInfo start, RetroBatConfig config, bool isExternalLauncher)
        {
            try
            {
                SimpleLogger.Instance.Info("Launching " + start.FileName + " " + start.Arguments);

                var exe = Process.Start(start);
                if (exe == null)
                {
                    SimpleLogger.Instance.Error("Failed to start EmulationStation process.");
                    return false;
                }

                int maxWaitMs = 10000;
                int intervalMs = 50;
                int waited = 0;

                IntPtr esHandle = IntPtr.Zero;

                SimpleLogger.Instance.Info("Waiting for EmulationStation main window…");
                while (!exe.HasExited && esHandle == IntPtr.Zero && waited < maxWaitMs)
                {
                    Thread.Sleep(intervalMs);
                    waited += intervalMs;
                    exe.Refresh();
                    esHandle = exe.MainWindowHandle;

                    if (waited % 1000 == 0)
                        SimpleLogger.Instance.Info($"…still waiting ({waited / 1000}s)");
                }

                // The wait loop above is bounded by maxWaitMs, so we always reach this point;
                // close the splash here unconditionally instead of only on the success path,
                // so it can never linger on screen if the window handle is never found.
                SplashVideo.CloseBlackSplash();

                if (esHandle == IntPtr.Zero)
                {
                    SimpleLogger.Instance.Warning("EmulationStation window handle not detected (likely exclusive fullscreen). Skipping focus.");
                }

                if (esHandle != IntPtr.Zero && !isExternalLauncher)
                {
                    Thread.Sleep(300);

                    if (config.FocusDelay > 0)
                    {
                        Thread.Sleep(config.FocusDelay);
                    }

                    FocusHelper.BringProcessWindowToFront(exe);
                }
                else
                {
                    if (exe.HasExited)
                        SimpleLogger.Instance.Error("EmulationStation process exited before creating a window.");
                    else
                        SimpleLogger.Instance.Warning("EmulationStation process is running but no main window detected.");
                }

                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to start EmulationStation: " + ex.Message);
                return false;
            }
            finally
            {
                // Safety net: guarantees the splash never stays up even if an exception is
                // thrown before the loop above gets a chance to close it (SplashVideo.CloseBlackSplash
                // is idempotent, so this is harmless on the normal success path too).
                SplashVideo.CloseBlackSplash();
            }
        }
    }
}
