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

            var videoDone = new ManualResetEvent(false);

            var thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new VideoPlayerForm(videoFile, esPath, config.GamepadVideoKill, config.KillVideoWhenESReady))
                {
                    form.FormClosed += (s, e) =>
                    {
                        videoDone.Set();
                    };
                    Application.Run(form);
                }
            });

            thread.SetApartmentState(ApartmentState.STA); // STA is required for WPF interop
            thread.Start();

            if (config.WaitForVideoEnd)
            {
                videoDone.WaitOne();
            }
        }
    }
}
