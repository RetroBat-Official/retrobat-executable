using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace RetroBat
{
    internal class SplashVideo
    {
        public static void RunIntroVideo(RetroBatConfig config, string esPath)
        {
            if (!config.EnableIntro)
                return;

            SimpleLogger.Instance.Info("Trying to run Intro Video.");

            string videoPath = Path.Combine(esPath, ".emulationstation", "video");

            if (config.FilePath != "default")
            {
                string customVideoPath = config.FilePath;
                if (!Directory.Exists(customVideoPath))
                    SimpleLogger.Instance.Warning("Custom video path does not exist: " + customVideoPath);
                else
                    videoPath = customVideoPath;
            }

            if (!Directory.Exists(videoPath))
            {
                SimpleLogger.Instance.Warning("Video directory does not exist: " + videoPath);
                return;
            }

            string[] videoFiles = Directory.GetFiles(videoPath, "*.mp4", SearchOption.AllDirectories);

            if (videoFiles.Length == 0)
            {
                SimpleLogger.Instance.Warning("No video files found in: " + videoPath);
                return;
            }

            string videoFile = Path.Combine(videoPath, config.FileName);

            if (config.RandomVideo)
            {
                SimpleLogger.Instance.Info("Getting random video file from: " + videoPath);
                Random rand = new Random();
                int index = rand.Next(videoFiles.Length);
                videoFile = videoFiles[index];
            }

            if (!File.Exists(videoFile))
            {
                SimpleLogger.Instance.Warning("Video file does not exist: " + videoFile);
                return;
            }

            SimpleLogger.Instance.Info("Video file to play: " + videoFile);

            var thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new VideoPlayerForm(videoFile, esPath, config.GamepadVideoKill, config.KillVideoWhenESReady));
            });

            thread.SetApartmentState(ApartmentState.STA); // STA is required for WPF interop
            thread.Start();
            /*

            int videoduration = config.VideoDuration;
            SimpleLogger.Instance.Info("Video duration set to: " + videoduration.ToString());

            List<string> commandArray = new List<string>
            {
                "--video",
                "\"" + videoFile + "\""
            };

            string args = string.Join(" ", commandArray);
            string exeES = Path.Combine(esPath, "emulationstation.exe");

            var start = new ProcessStartInfo()
            {
                FileName = exeES,
                WorkingDirectory = esPath,
                Arguments = args,
                UseShellExecute = false
            };

            if (start == null)
                return;

            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount);
            
            if (config.Autostart && uptime.TotalSeconds < 30)
            {
                SimpleLogger.Instance.Info("RetroBat set to run at startup, adding a 6 seconds delay.");
                System.Threading.Thread.Sleep(6000);
            }

            try
            {
                var p = Process.Start(start);

                if (p == null)
                {
                    SimpleLogger.Instance.Warning("Process failed to start.");
                    return;
                }

                var inputThread = new Thread(() =>
                {
                    Thread.Sleep(200);

                    while (!p.HasExited)
                    {
                        bool inputDetected = keysToCheck.Any(k => GetAsyncKeyState(k) < 0);
                        bool gamepadButtonPressed = false;

                        if (inputDetected || gamepadButtonPressed)
                        {
                            SimpleLogger.Instance.Info("Input detected. Killing video process.");
                            try { p.Kill(); } catch { }
                            break;
                        }

                        Thread.Sleep(100);
                    }
                });

                inputThread.IsBackground = true;
                inputThread.Start();
                
                // Wait for duration or exit
                if (videoduration < 1000)
                    p.WaitForExit();
                else
                    p.WaitForExit(videoduration + 1000);
                
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to start EmulationStation video: " + ex.Message);
            }*/
        }
    }
}
