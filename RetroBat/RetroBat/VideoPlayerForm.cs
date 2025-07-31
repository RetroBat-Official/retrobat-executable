using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;

namespace RetroBat
{
    public class VideoPlayerForm : RawInputForm
    {
        private ElementHost _elementHost;
        private MediaElement _mediaElement;
        private string _path;

        private bool _gamepadKill;
        private bool _letVideoRun;

        public VideoPlayerForm(string videoPath, string path, bool gamepadKill = false, bool killVideoWhenESReady = false)
        {
            _gamepadKill = gamepadKill;
            _letVideoRun = !killVideoWhenESReady;
            _path = System.IO.Path.Combine(path, ".emulationstation", "tmp", "emulationstation.ready");
            if (File.Exists(_path))
            {
                try { File.Delete(_path); }
                catch { }
            }

            this.BackColor = System.Drawing.Color.Black;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.ShowInTaskbar = false;
            this.TopLevel = true;
            this.TopMost = true;
            this.Opacity = 0;
            this.KeyPreview = true;

#if DEBUG            
            this.WindowState = FormWindowState.Normal;
            this.Width = 1280;
            this.Height = 768;
#endif
            _elementHost = new ElementHost
            {
                Dock = DockStyle.Fill
            };

            _mediaElement = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.Uniform,
                Source = new Uri(videoPath, UriKind.Absolute)
            };

            _mediaElement.Focusable = false;
            _mediaElement.MediaOpened += (s, e) => this.Opacity = 1;
            _mediaElement.MediaEnded += (s, e) => { _timer.Stop(); this.Close(); };
            _mediaElement.MediaFailed += (s, e) => { _timer.Stop(); this.Close(); };

            _elementHost.Child = _mediaElement;
            this.Controls.Add(_elementHost);
                     
            this.Load += (s, e) =>
            {
                _mediaElement.Play();

                _timer = new System.Windows.Forms.Timer() { Interval = 2 };
                _timer.Tick += OnTimer;
                _timer.Start();

                //_ticks = Environment.TickCount;
            };
        }

        private RawInputForm _rawInput;

        private void OnTimer(object sender, EventArgs e)
        {
            if (this.IsHandleCreated && this.Handle != GetForegroundWindow())
            {
                SetForegroundWindow(this.Handle);
                SetActiveWindow(this.Handle);
            }

            bool gamepadButtonPressed = _gamepadKill && RawInputDetected;
            bool inputDetected = keysToCheck.Any(k => GetAsyncKeyState(k) < 0);
            bool fileTriggered = File.Exists(_path) && !_letVideoRun;

            if (inputDetected || gamepadButtonPressed || fileTriggered)
            {
                this.BeginInvoke(new Action(() =>
                {
                    if (gamepadButtonPressed)
                        SimpleLogger.Instance.Info("Gamepad input detected, killing video process.");
                    else if (inputDetected)
                        SimpleLogger.Instance.Info("Keyboard or mouse input detected. Killing video process.");
                    else if (fileTriggered)
                        SimpleLogger.Instance.Info("File trigger detected. Killing video process.");
                    else
                        SimpleLogger.Instance.Info("Duration reached. Killing video process.");

                    _mediaElement?.Stop();
                    Close();
                }));
            }
        }

        private System.Windows.Forms.Timer _timer;

        protected override void Dispose(bool disposing)
        {
            _rawInput?.Dispose();
            _rawInput = null;

            _timer?.Dispose();
            _timer = null;

            base.Dispose(disposing);

            var exe = Process.GetProcessesByName("emulationstation").FirstOrDefault();
            if (exe != null)
                FocusHelper.BringProcessWindowToFrontWithRetry(exe);
        }

        #region Apis
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

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

        private int[] keysToCheck = new int[] { VK_LBUTTON, VK_RBUTTON, VK_SPACE, VK_ESCAPE, VK_ENTER, VK_UP, VK_DOWN, VK_LEFT, VK_RIGHT, VK_W, VK_A, VK_S, VK_D };
        #endregion
    }

}
