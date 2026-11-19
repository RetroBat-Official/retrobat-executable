using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RetroBat
{
    /// <summary>
    /// Arcade cabinet mode (DisableWindowsInterface=3): blocks the Windows keys and the shortcuts
    /// that leave the frontend until EmulationStation exits: Win (Start menu and every Win+ shortcut),
    /// Alt+Tab, Alt+Esc, Ctrl+Esc, Ctrl+Shift+Esc, and Alt+F4 while EmulationStation has the focus.
    /// Nothing is changed on the system: the hook goes away with the process.
    /// Ctrl+Alt+Del and Win+L are handled by Windows itself and cannot be blocked.
    /// </summary>
    internal static class KioskKeyboardLock
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int HC_ACTION = 0;
        private const uint LLKHF_ALTDOWN = 0x20;

        private const int VK_TAB = 0x09;
        private const int VK_CONTROL = 0x11;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_F4 = 0x73;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Kept in a field: the garbage collector must not free the delegate while the hook is installed
        private static LowLevelKeyboardProc _proc;
        private static IntPtr _hook = IntPtr.Zero;
        private static int _frontendProcessId;

        /// <summary>Installs the hook and returns when EmulationStation exits. Falls back to a plain wait
        /// if the hook cannot be installed.</summary>
        public static void RunUntilExit(Process esInstance)
        {
            _frontendProcessId = esInstance.Id;
            _proc = HookCallback;

            using (Process current = Process.GetCurrentProcess())
            using (ProcessModule module = current.MainModule)
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);

            if (_hook == IntPtr.Zero)
            {
                SimpleLogger.Instance.Warning("Could not install the keyboard lock (error " + Marshal.GetLastWin32Error() + "), waiting without it.");
                esInstance.WaitForExit();
                return;
            }

            SimpleLogger.Instance.Info("Keyboard lock enabled: Windows keys and exit shortcuts are blocked.");

            try
            {
                // A low-level hook is called through the message loop of the thread that installed it
                using (var context = new ApplicationContext())
                using (var timer = new Timer { Interval = 500 })
                {
                    timer.Tick += (sender, e) =>
                    {
                        if (esInstance.HasExited)
                            context.ExitThread();
                    };

                    timer.Start();

                    if (!esInstance.HasExited)
                        Application.Run(context);
                }
            }
            finally
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
                SimpleLogger.Instance.Info("Keyboard lock removed.");
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION)
            {
                var key = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                if (IsBlocked(key))
                    return (IntPtr)1;
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool IsBlocked(KBDLLHOOKSTRUCT key)
        {
            bool alt = (key.flags & LLKHF_ALTDOWN) != 0;
            bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

            switch ((int)key.vkCode)
            {
                case VK_LWIN:
                case VK_RWIN:
                    return true;                        // Start menu, Win+G, Win+Tab, Win+D...
                case VK_TAB:
                    return alt;                         // Alt+Tab
                case VK_ESCAPE:
                    return alt || ctrl;                 // Alt+Esc, Ctrl+Esc, Ctrl+Shift+Esc (Task Manager)
                case VK_F4:
                    return alt && IsFrontendFocused();  // Alt+F4 would close EmulationStation
            }

            return false;
        }

        private static bool IsFrontendFocused()
        {
            uint processId;
            GetWindowThreadProcessId(GetForegroundWindow(), out processId);
            return processId == _frontendProcessId;
        }
    }
}