using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RealTimeUdpStream.Core.Input
{
    /// <summary>
    /// Giả lập keyboard input vào toàn hệ thống (CLIENT side)
    /// </summary>
    public class KeyboardSimulator : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern ushort MapVirtualKey(uint uCode, uint uMapType);

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        private readonly Dictionary<VirtualKey, VirtualKey> _keyMapping;
        private bool _disposed = false;

        public KeyboardSimulator(Dictionary<VirtualKey, VirtualKey> keyMapping = null)
        {
            if (keyMapping == null)
            {
                // Ánh xạ mặc định: WASD (Client) -> TFGH (Host)
                _keyMapping = new Dictionary<VirtualKey, VirtualKey>
                {
                    { VirtualKey.W, VirtualKey.T },
                    { VirtualKey.A, VirtualKey.F },
                    { VirtualKey.S, VirtualKey.G },
                    { VirtualKey.D, VirtualKey.H },
                    { VirtualKey.Space, VirtualKey.Space },
                    { VirtualKey.Shift, VirtualKey.Shift },
                    { VirtualKey.Ctrl, VirtualKey.Ctrl }
                };
                Console.WriteLine("[KeyboardSimulator] Using DEFAULT key mapping (no config provided)");
                Debug.WriteLine("[KeyboardSimulator] Using DEFAULT key mapping (no config provided)");
            }
            else
            {
                _keyMapping = keyMapping;
                Console.WriteLine($"[KeyboardSimulator] Using CUSTOM key mapping ({keyMapping.Count} mappings)");
                Debug.WriteLine($"[KeyboardSimulator] Using CUSTOM key mapping ({keyMapping.Count} mappings)");
                foreach (var kvp in _keyMapping)
                {
                    Console.WriteLine($"  {kvp.Key} → {kvp.Value}");
                    Debug.WriteLine($"  {kvp.Key} → {kvp.Value}");
                }
            }
        }

        public void SimulateKeyEvent(KeyEvent keyEvent)
        {
            if (_disposed) return;

            try
            {
                // Ánh xạ phím
                VirtualKey targetKey = _keyMapping.ContainsKey(keyEvent.Key)
                    ? _keyMapping[keyEvent.Key]
                    : keyEvent.Key;

                Console.WriteLine($"[KeyboardSimulator] Mapping: {keyEvent.Key} -> {targetKey} (Action: {keyEvent.Action})");
                
                if (keyEvent.Action == KeyAction.Down)
                {
                    KeyDown(targetKey);
                    Console.WriteLine($"[KeyboardSimulator] ✓ Pressed: {targetKey}");
                }
                else
                {
                    KeyUp(targetKey);
                    Console.WriteLine($"[KeyboardSimulator] ✓ Released: {targetKey}");
                }

                Debug.WriteLine($"[KeyboardSimulator] Simulated {targetKey} {keyEvent.Action}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeyboardSimulator] Loi: {ex.Message}");
                Debug.WriteLine($"[KeyboardSimulator] Error: {ex.Message}");
            }
        }

        private void KeyDown(VirtualKey key)
        {
            SendKeyInput(key, KEYEVENTF_KEYDOWN);
        }

        private void KeyUp(VirtualKey key)
        {
            SendKeyInput(key, KEYEVENTF_KEYUP);
        }

        private void SendKeyInput(VirtualKey key, uint flags)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = (ushort)key;
            inputs[0].u.ki.wScan = 0;
            inputs[0].u.ki.dwFlags = flags;
            inputs[0].u.ki.time = 0;
            inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }

        #region Windows API Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        #endregion
    }
}
