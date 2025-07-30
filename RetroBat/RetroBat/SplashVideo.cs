using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RetroBat
{
    internal class SplashVideo
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        public static volatile bool ControllerInputDetected = false;


        public static void RunIntroVideo(RetroBatConfig config, string esPath)
        {
            SimpleLogger.Instance.Info("Running IntroVideo.");

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

            SimpleLogger.Instance.Info("Video file played: " + videoPath);

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

            const int VK_LBUTTON = 0x01;
            const int VK_RBUTTON = 0x02;
            const int VK_SPACE = 0x20;
            const int VK_ESCAPE = 0x1B;
            const int VK_ENTER = 0x0D;
            const int VK_UP = 0x26;
            const int VK_DOWN = 0x28;
            const int VK_LEFT = 0x25;
            const int VK_RIGHT = 0x27;
            const int VK_W = 0x57;
            const int VK_A = 0x41;
            const int VK_S = 0x53;
            const int VK_D = 0x44;

            int[] keysToCheck = new int[] { VK_LBUTTON, VK_RBUTTON, VK_SPACE, VK_ESCAPE, VK_ENTER, VK_UP, VK_DOWN, VK_LEFT, VK_RIGHT, VK_W, VK_A, VK_S, VK_D };

            try
            {
                var inputFormThread = new Thread(() =>
                {
                    Application.Run(new RawInputForm());
                });
                inputFormThread.IsBackground = true;
                inputFormThread.SetApartmentState(ApartmentState.STA);
                inputFormThread.Start();

                var p = Process.Start(start);

                if (p == null)
                {
                    SimpleLogger.Instance.Warning("Process failed to start.");
                    return;
                }

                var inputThread = new Thread(() =>
                {
                    Thread.Sleep(1500); // Wait 1.5 seconds before listening

                    while (!p.HasExited)
                    {
                        bool inputDetected = keysToCheck.Any(k => GetAsyncKeyState(k) < 0);
                        bool gamepadButtonPressed = config.GamepadVideoKill ? SplashVideo.ControllerInputDetected : false;

                        if (inputDetected || gamepadButtonPressed)
                        {
                            if (gamepadButtonPressed)
                                SimpleLogger.Instance.Info("Gamepad input detected, killing video process.");
                            else
                                SimpleLogger.Instance.Info("Keyboard or mouse input detected. Killing video process.");
                            
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
            }
        }
    }
}
