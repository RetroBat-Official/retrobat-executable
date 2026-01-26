using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RetroBat
{
    internal class SplashVideo
    {
        private static Form _blackSplashForm;

        public static bool CanRunIntroVideo(RetroBatConfig config, string esPath)
        {
            try
            {
                if (!config.EnableIntro)
                    return false;

                string videoPath = Path.Combine(esPath, ".emulationstation", "video");

                if (!string.IsNullOrEmpty(config.FilePath) && config.FilePath != "default")
                    videoPath = config.FilePath;

                if (!Directory.Exists(videoPath))
                    return false;

                return Directory.EnumerateFiles(videoPath, "*.mp4", SearchOption.AllDirectories).Any();
            }
            catch
            {
                return false;
            }
        }

        public static void RunIntroVideo(RetroBatConfig config, string esPath, Screen targetScreen = null, bool externalLauncher = false)
        {
            bool canRunIntro = SplashVideo.CanRunIntroVideo(config, esPath);

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
                using (var form = new VideoPlayerForm(videoFile, esPath, config.GamepadVideoKill, config.KillVideoWhenESReady, targetScreen, externalLauncher))
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

                _blackSplashForm.Load += (s, e) =>
                {
                    try
                    {
                        _blackSplashForm.Focus();
                        _blackSplashForm.Activate();
                    }
                    catch { }
                };

                _blackSplashForm.Shown += (s, e) => splashDone.Set();
                
                var watchdog = new System.Windows.Forms.Timer();
                watchdog.Interval = 15000; // 15 secondes max
                watchdog.Tick += (s, e) =>
                {
                    try
                    {
                        watchdog.Stop();
                        _blackSplashForm?.Close();
                    }
                    catch { }
                };
                watchdog.Start();

                Application.Run(_blackSplashForm);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Wait until the form is actually shown
            splashDone.WaitOne();
        }

        public static void CloseBlackSplash()
        {
            try
            {
                var form = _blackSplashForm;
                if (form == null || form.IsDisposed)
                    return;

                if (form.InvokeRequired)
                {
                    form.Invoke(new Action(() =>
                    {
                        form.FormClosed += (s, e) => _blackSplashForm = null;
                        form.Close();
                    }));
                }
                else
                {
                    form.FormClosed += (s, e) => _blackSplashForm = null;
                    form.Close();
                }
            }
            catch
            {
                _blackSplashForm = null;
            }
        }
    }
}
