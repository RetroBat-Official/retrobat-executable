using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace RetroBat
{
    internal class SplashVideo
    {
        private static Form _blackSplashForm;
        public static void RunIntroVideo(RetroBatConfig config, string esPath, Screen targetScreen = null)
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
                using (var form = new VideoPlayerForm(videoFile, esPath, config.GamepadVideoKill, config.KillVideoWhenESReady, targetScreen))
                {
                    form.FormClosed += (s, e) =>
                    {
                        videoDone.Set();
                    };
                    Application.Run(form);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (config.WaitForVideoEnd)
            {
                videoDone.WaitOne();
            }
        }
        public static void ShowBlackSplash(Screen targetScreen = null)
        {
            if (_blackSplashForm != null)
                return;

            var splashDone = new ManualResetEvent(false);

            var thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var screen = targetScreen ?? Screen.PrimaryScreen;

                _blackSplashForm = new Form
                {
                    BackColor = System.Drawing.Color.Black,
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    Bounds = screen.Bounds,
                    TopMost = true,
                    ShowInTaskbar = false
                };

                _blackSplashForm.Shown += (s, e) => splashDone.Set();
                Application.Run(_blackSplashForm);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Wait until the form is actually shown
            splashDone.WaitOne();
        }

        public static void CloseBlackSplash()
        {
            if (_blackSplashForm == null || _blackSplashForm.IsDisposed)
                return;

            try
            {
                if (_blackSplashForm.IsHandleCreated)
                {
                    _blackSplashForm.BeginInvoke(new Action(() =>
                    {
                        _blackSplashForm.Close();
                        _blackSplashForm = null;
                    }));
                }
            }
            catch { }
        }
    }
}
