using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;

namespace RetroBat
{
    public class VideoPlayerForm : Form
    {
        private ElementHost _elementHost;
        private MediaElement _mediaElement;
        private string _path;

        private bool _gamepadKill;
        private bool _letVideoRun;
        private bool _externalLauncher;
        public bool _mediaEnded = false;

        public VideoPlayerForm(string videoPath, string path, bool gamepadKill = false, bool killVideoWhenESReady = false, Screen targetScreen = null, bool externalLauncher = false)
        {
            _externalLauncher = externalLauncher;
            _gamepadKill = gamepadKill;
            _letVideoRun = !killVideoWhenESReady;
            _path = System.IO.Path.Combine(path, ".emulationstation", "tmp", "emulationstation.ready");
            if (File.Exists(_path))
            {
                try { File.Delete(_path); }
                catch { }
            }

            var screen = targetScreen ?? Screen.PrimaryScreen;

            this.BackColor = System.Drawing.Color.Black;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = screen.Bounds;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.TopLevel = true;
            this.Opacity = 0;
            this.KeyPreview = true;
            this.WindowState = FormWindowState.Normal;

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
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Source = new Uri(videoPath, UriKind.Absolute)
            };

            _mediaElement.Focusable = false;
            
            _mediaElement.MediaOpened += (s, e) =>
            {
                ForceForeground();
                SimpleLogger.Instance.Info("Media opened.");
                this.Opacity = 1;
                this.TopMost = true;
                this.Activate();
                this.BringToFront();
            };

            _mediaElement.MediaEnded += (s, e) => 
            {
                SimpleLogger.Instance.Info("Media Ended.");
                _mediaEnded = true;
                _timer.Stop();
                this.Close();
            };

            _mediaElement.MediaFailed += (s, e) =>
            {
                SimpleLogger.Instance.Warning($"Media failed: {e.ErrorException?.Message}");
                _timer.Stop();
                this.Close();
            };

            _elementHost.Dock = DockStyle.Fill;

            _elementHost.Child = _mediaElement;
            this.Controls.Add(_elementHost);
                     
            this.Load += (s, e) =>
            {
                try
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.Bounds = screen.Bounds;
                    this.WindowState = FormWindowState.Normal;

                    System.Threading.Thread.Sleep(100);
                    _mediaElement.Play();
                    SimpleLogger.Instance.Info("Video started.");
                    this.TopMost = true;
                    this.Activate();
                    this.BringToFront();
                    _timer = new System.Windows.Forms.Timer() { Interval = 50 };
                    _timer.Tick += OnTimer;
                    _timer.Start();
                }
                catch (Exception ex) { SimpleLogger.Instance.Warning("MediaElement failed to launch" + ex); }
            };

            this.Shown += (s, e) =>
            {
                ForceForeground();
                this.TopMost = true;
                this.BringToFront();
            };
        }

        private void OnTimer(object sender, EventArgs e)
        {
            bool gamepadButtonPressed = _gamepadKill && XInput.IsFaceButtonPressed();
            bool inputDetected = keysToCheck.Any(k => GetAsyncKeyState(k) < 0);
            bool fileTriggered = File.Exists(_path) && !_letVideoRun;

            if (inputDetected || gamepadButtonPressed || fileTriggered)
            {
                if (gamepadButtonPressed)
                    SimpleLogger.Instance.Info("Gamepad input detected, killing video process.");
                else if (inputDetected)
                    SimpleLogger.Instance.Info("Keyboard or mouse input detected. Killing video process.");
                else if (fileTriggered)
                {
                    SimpleLogger.Instance.Info("EmulationStation ready. Killing video process.");
                    System.Threading.Thread.Sleep(200);
                }

                _timer?.Dispose();
                _timer = null;
                _mediaElement?.Stop();
                SimpleLogger.Instance.Info("Video stopped.");
                _mediaElement?.Close();
                _mediaElement = null;
                Close();
            }
        }

        private System.Windows.Forms.Timer _timer;

        protected override void Dispose(bool disposing)
        {
            _timer?.Dispose();
            _timer = null;

            base.Dispose(disposing);

            if (!_externalLauncher)
            {
                for (int i = 0; i < 5; i++)
                {
                    var exe = Process.GetProcessesByName("emulationstation").FirstOrDefault();
                    if (exe != null)
                    {
                        SimpleLogger.Instance.Info("Restoring focus to EmulationStation...");
                        FocusHelper.BringProcessWindowToFront(exe, attempts: 5, delayMs: 250);
                        break;
                    }

                    System.Threading.Thread.Sleep(300);
                }
            }
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

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        private const int SW_SHOW = 5;

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

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        private void ForceForeground()
        {
            IntPtr hWnd = this.Handle;
            if (hWnd == IntPtr.Zero)
                return;

            IntPtr fgWnd = GetForegroundWindow();
            if (hWnd == fgWnd)
                return;

            uint fgThread = GetWindowThreadProcessId(fgWnd, out _);
            uint thisThread = GetCurrentThreadId();

            // Temporarily attach input threads
            AttachThreadInput(thisThread, fgThread, true);

            ShowWindow(hWnd, SW_SHOW);
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetForegroundWindow(hWnd);
            SetActiveWindow(hWnd);
            BringWindowToTop(hWnd);

            // Detach
            AttachThreadInput(thisThread, fgThread, false);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_SHOWWINDOW = 0x0040;

        private void ToggleTopMost()
        {
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            System.Threading.Thread.Sleep(10);
            SetWindowPos(this.Handle, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
    }
}
