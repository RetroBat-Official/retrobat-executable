using System;
using System.Runtime.InteropServices;

namespace RetroBat
{
    public static class XInput
    {
        // Face button bit masks
        private const ushort XINPUT_GAMEPAD_A = 0x1000;
        private const ushort XINPUT_GAMEPAD_B = 0x2000;
        private const ushort XINPUT_GAMEPAD_X = 0x4000;
        private const ushort XINPUT_GAMEPAD_Y = 0x8000;

        // XInput gamepad structure
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        // XInput state structure
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        // Import XInputGetState from Windows
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

        /// <summary>
        /// Checks if any face button (A, B, X, Y) is pressed on any connected XInput controller.
        /// </summary>
        public static bool IsFaceButtonPressed()
        {
            for (uint i = 0; i < 4; i++) // Supports up to 4 controllers
            {
                if (XInputGetState(i, out XINPUT_STATE state) == 0) // 0 = Success
                {
                    ushort buttons = state.Gamepad.wButtons;
                    if ((buttons & (XINPUT_GAMEPAD_A | XINPUT_GAMEPAD_B | XINPUT_GAMEPAD_X | XINPUT_GAMEPAD_Y)) != 0)
                        return true;
                }
            }
            return false;
        }
    }
}
