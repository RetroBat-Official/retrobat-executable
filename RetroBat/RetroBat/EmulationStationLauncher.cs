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

        /// <summary>Starts EmulationStation and waits for/restores focus on its window. Returns false if the process failed to start.</summary>
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

                if (esHandle == IntPtr.Zero)
                {
                    SimpleLogger.Instance.Warning("EmulationStation window handle not detected (likely exclusive fullscreen). Skipping focus.");
                }

                if (esHandle != IntPtr.Zero && !isExternalLauncher)
                {
                    SplashVideo.CloseBlackSplash();
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
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to start EmulationStation: " + ex.Message);
            }

            return true;
        }
    }
}
