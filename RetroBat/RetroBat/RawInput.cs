using RetroBat;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

namespace RetroBat
{
    public abstract class RawInputForm : Form
    {
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            SimpleLogger.Instance.Info("RawInputForm started, registering raw input devices...");

            // Register to receive raw input from gamepads (usage page 1, usage 5 = gamepad)
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;  // Generic Desktop Controls
            rid[0].usUsage = 0x05;      // Gamepad (use 0x04 for Joystick)
            rid[0].dwFlags = RIDEV_INPUTSINK; // Receive input even if not focused
            rid[0].hwndTarget = this.Handle;

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
                SimpleLogger.Instance.Warning("Failed to register raw input device(s).");
            else
                SimpleLogger.Instance.Info("Registered raw input device(s) successfully.");
        }

        protected bool RawInputDetected { get; private set; }
      
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT)
            {
                uint dwSize = 0;
                // First get the size of the raw input data
                GetRawInputData(m.LParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                if (dwSize > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                    try
                    {
                        uint readSize = GetRawInputData(m.LParam, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                        if (readSize == dwSize)
                        {
                            // Read the header
                            RAWINPUTHEADER header = (RAWINPUTHEADER)Marshal.PtrToStructure(buffer, typeof(RAWINPUTHEADER));

                            if (header.dwType == RIM_TYPEHID)
                            {
                                IntPtr pRawHidData = IntPtr.Add(buffer, Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                                RAWHID rawHid = (RAWHID)Marshal.PtrToStructure(pRawHidData, typeof(RAWHID));
                                IntPtr pRawData = IntPtr.Add(pRawHidData, Marshal.SizeOf(typeof(RAWHID)));

                                int rawDataLength = (int)(rawHid.dwSizeHid * rawHid.dwCount);
                                byte[] rawData = new byte[rawDataLength];
                                Marshal.Copy(pRawData, rawData, 0, rawDataLength);

                                string rawDataStr = "Raw HID data bytes: ";
                                for (int i = 0; i < Math.Min(16, rawDataLength); i++)
                                    rawDataStr += $"{rawData[i]:X2} ";

                                bool gamepadButtonPressed = false;
                                for (int i = 0; i < rawDataLength; i++)
                                {
                                    if (rawData[i] != 0)
                                    {
                                        gamepadButtonPressed = true;
                                        break;
                                    }
                                }

                                if (gamepadButtonPressed)
                                    RawInputDetected = true;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            base.WndProc(ref m);
        }

        #region Api
        // Constants
        const int WM_INPUT = 0x00FF;
        const uint RID_INPUT = 0x10000003;
        const uint RIM_TYPEHID = 2;
        const uint RIDEV_INPUTSINK = 0x00000100;

        // P/Invoke declarations
        [DllImport("User32.dll")]
        static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("User32.dll")]
        static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
     
        // Structures
        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
            // Followed by variable length raw data, handled manually
        }
        #endregion
    }
}