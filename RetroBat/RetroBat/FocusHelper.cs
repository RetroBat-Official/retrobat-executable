using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace RetroBat
{
    internal class FocusHelper
    {
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int dwProcessId);

        const int SW_MINIMIZE = 6;
        const int SW_RESTORE = 9;

        public static bool BringProcessWindowToFrontWithRetry(Process proc, int attempts = 2, int delayMs = 800)
        {
            bool success = false;
            for (int i = 0; i < attempts; i++)
            {
                success = BringProcessWindowToFront(proc);
                if (success)
                    SimpleLogger.Instance.Info($"EmulationStation window is now in the foreground (attempt #{i + 1}).");
                else
                    SimpleLogger.Instance.Warning($"Failed to bring EmulationStation window to front (attempt #{i + 1}).");

                if (success && i == attempts - 1)
                    break;  // last attempt, success done
                else if (!success && i == attempts - 1)
                    break;  // last attempt, no success, stop

                Thread.Sleep(delayMs);
            }
            return success;
        }

        public static bool BringProcessWindowToFront(Process proc, int maxAttempts = 5, int delayMs = 800)
        {
            if (proc == null) return false;

            if (!proc.WaitForInputIdle(5000))
                return false;

            proc.Refresh();
            IntPtr hWnd = proc.MainWindowHandle;
            if (hWnd == IntPtr.Zero)
                return false;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Optionally minimize and restore to force attention
                if (attempt == 1)
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }
                else
                {
                    ShowWindow(hWnd, SW_MINIMIZE);
                    Thread.Sleep(5);
                    ShowWindow(hWnd, SW_RESTORE);
                }

                AllowSetForegroundWindow(proc.Id);
                bool success = SetForegroundWindow(hWnd);

                if (success)
                {
                    //FlashWindow(hWnd, true);
                    SimpleLogger.Instance.Info($"EmulationStation window brought to front on attempt #{attempt}.");
                    return true;
                }

                SimpleLogger.Instance.Warning($"Attempt #{attempt} to bring window to front failed.");
                Thread.Sleep(delayMs);
            }

            SimpleLogger.Instance.Warning("Failed to bring window to front after multiple attempts.");
            return false;
        }
    }
}
