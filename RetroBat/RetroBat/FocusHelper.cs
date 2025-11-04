using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace RetroBat
{
    internal class FocusHelper
    {
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool SetActiveWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        const int SW_RESTORE = 9;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_SHOWWINDOW = 0x0040;

        public static bool BringProcessWindowToFront(Process proc, int attempts = 5, int delayMs = 300)
        {
            if (proc == null)
                return false;

            try
            {
                if (!proc.WaitForInputIdle(5000))
                    SimpleLogger.Instance.Warning("WaitForInputIdle timed out.");

                for (int i = 0; i < attempts; i++)
                {
                    proc.Refresh();
                    IntPtr hWnd = proc.MainWindowHandle;
                    if (hWnd == IntPtr.Zero)
                    {
                        SimpleLogger.Instance.Warning($"Attempt #{i + 1}: Window handle not yet available.");
                        Thread.Sleep(delayMs);
                        continue;
                    }

                    if (ForceForeground(hWnd))
                    {
                        SimpleLogger.Instance.Info($"Window brought to front on attempt #{i + 1}.");
                        return true;
                    }

                    Thread.Sleep(delayMs);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("BringProcessWindowToFront exception: " + ex.Message);
            }

            SimpleLogger.Instance.Warning("Failed to bring process window to front after retries.");
            return false;
        }

        public static bool ForceForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return false;

            try
            {
                IntPtr fgWnd = GetForegroundWindow();
                if (fgWnd == hWnd)
                {
                    SimpleLogger.Instance.Info("Window already in foreground.");
                    return true;
                }

                uint thisThread = GetCurrentThreadId();
                uint targetThread = GetWindowThreadProcessId(hWnd, out _);

                // Temporarily share input queues
                AttachThreadInput(thisThread, targetThread, true);

                // Ensure window is visible and restored
                ShowWindow(hWnd, SW_RESTORE);
                Thread.Sleep(100);

                bool result = SetForegroundWindow(hWnd);
                BringWindowToTop(hWnd);
                SetActiveWindow(hWnd);

                // Toggle topmost trick if normal method failed
                if (!result)
                {
                    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    Thread.Sleep(50);
                    SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    result = SetForegroundWindow(hWnd);
                }

                AttachThreadInput(thisThread, targetThread, false);

                SimpleLogger.Instance.Info($"ForceForeground result = {result}");
                return result;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("ForceForeground exception: " + ex.Message);
                return false;
            }
        }

        public static void ToggleTopMost(IntPtr hWnd)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            Thread.Sleep(50);
            SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
    }
}
