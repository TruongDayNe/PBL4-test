using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RealTimeUdpStream.Core.Input
{
    /// <summary>
    /// Capture keyboard input từ toàn hệ thống (HOST side)
    /// </summary>
    public class KeyboardCapture : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private readonly Dictionary<VirtualKey, bool> _previousKeyStates;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isCapturing = false;
        private bool _disposed = false;

        public event Action<KeyEvent> OnKeyEvent;

        public KeyboardCapture()
        {
            // Khởi tạo tất cả phím thường dùng cho game streaming
            _previousKeyStates = new Dictionary<VirtualKey, bool>
            {
                // Movement keys (WASD)
                { VirtualKey.W, false },
                { VirtualKey.A, false },
                { VirtualKey.S, false },
                { VirtualKey.D, false },
                
                // Action keys (QERTFGCVXZB)
                { VirtualKey.Q, false },
                { VirtualKey.E, false },
                { VirtualKey.R, false },
                { VirtualKey.T, false },
                { VirtualKey.F, false },
                { VirtualKey.G, false },
                { VirtualKey.C, false },
                { VirtualKey.V, false },
                { VirtualKey.X, false },
                { VirtualKey.Z, false },
                { VirtualKey.B, false },
                { VirtualKey.H, false },
                
                // Additional keys for controller mapping (IJKLMNOPUY)
                { VirtualKey.I, false },
                { VirtualKey.J, false },
                { VirtualKey.K, false },
                { VirtualKey.L, false },
                { VirtualKey.M, false },
                { VirtualKey.N, false },
                { VirtualKey.O, false },
                { VirtualKey.P, false },
                { VirtualKey.U, false },
                { VirtualKey.Y, false },
                
                // Modifier keys (Left/Right variants)
                { VirtualKey.LShift, false },
                { VirtualKey.RShift, false },
                { VirtualKey.LCtrl, false },
                { VirtualKey.RCtrl, false },
                { VirtualKey.LAlt, false },
                { VirtualKey.RAlt, false },
                { VirtualKey.Space, false },
                { VirtualKey.Tab, false },
                { VirtualKey.CapsLock, false },
                { VirtualKey.Enter, false },
                { VirtualKey.Escape, false },
                
                // Number keys (1-0)
                { VirtualKey.D1, false },
                { VirtualKey.D2, false },
                { VirtualKey.D3, false },
                { VirtualKey.D4, false },
                { VirtualKey.D5, false },
                { VirtualKey.D6, false },
                { VirtualKey.D7, false },
                { VirtualKey.D8, false },
                { VirtualKey.D9, false },
                { VirtualKey.D0, false },
                
                // Arrow keys
                { VirtualKey.Left, false },
                { VirtualKey.Up, false },
                { VirtualKey.Right, false },
                { VirtualKey.Down, false },
                
                // Numpad keys
                { VirtualKey.NumPad0, false },
                { VirtualKey.NumPad1, false },
                { VirtualKey.NumPad2, false },
                { VirtualKey.NumPad3, false },
                { VirtualKey.NumPad4, false },
                { VirtualKey.NumPad5, false },
                { VirtualKey.NumPad6, false },
                { VirtualKey.NumPad7, false },
                { VirtualKey.NumPad8, false },
                { VirtualKey.NumPad9, false },
                { VirtualKey.Multiply, false },
                { VirtualKey.Add, false },
                { VirtualKey.Subtract, false },
                { VirtualKey.Decimal, false },
                { VirtualKey.Divide, false },
                
                // Function keys (F1-F12)
                { VirtualKey.F1, false },
                { VirtualKey.F2, false },
                { VirtualKey.F3, false },
                { VirtualKey.F4, false },
                { VirtualKey.F5, false },
                { VirtualKey.F6, false },
                { VirtualKey.F7, false },
                { VirtualKey.F8, false },
                { VirtualKey.F9, false },
                { VirtualKey.F10, false },
                { VirtualKey.F11, false },
                { VirtualKey.F12, false },
                
                // Special keys
                { VirtualKey.PageUp, false },
                { VirtualKey.PageDown, false },
                { VirtualKey.Home, false },
                { VirtualKey.End, false },
                { VirtualKey.Insert, false },
                { VirtualKey.Delete, false },
                { VirtualKey.Back, false },
                { VirtualKey.NumLock, false },
                { VirtualKey.ScrollLock, false },
                { VirtualKey.Pause, false },
                { VirtualKey.PrintScreen, false },
                
                // OEM keys (symbols)
                { VirtualKey.OemSemicolon, false },
                { VirtualKey.OemPlus, false },
                { VirtualKey.OemComma, false },
                { VirtualKey.OemMinus, false },
                { VirtualKey.OemPeriod, false },
                { VirtualKey.OemQuestion, false },
                { VirtualKey.OemTilde, false },
                { VirtualKey.OemOpenBrackets, false },
                { VirtualKey.OemPipe, false },
                { VirtualKey.OemCloseBrackets, false },
                { VirtualKey.OemQuotes, false }
            };
        }

        public void StartCapture()
        {
            if (_isCapturing || _disposed) return;

            _isCapturing = true;
            _cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => CaptureLoop(_cancellationTokenSource.Token));
            Debug.WriteLine("[KeyboardCapture] Started capturing keyboard input");
        }

        public void StopCapture()
        {
            if (!_isCapturing) return;

            _cancellationTokenSource?.Cancel();
            _isCapturing = false;
            Debug.WriteLine("[KeyboardCapture] Stopped capturing keyboard input");
        }

        private async Task CaptureLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (var key in _previousKeyStates.Keys)
                    {
                        bool isPressed = IsKeyPressed((int)key);
                        bool wasPressedBefore = _previousKeyStates[key];

                        if (isPressed && !wasPressedBefore)
                        {
                            // Key DOWN
                            var keyEvent = new KeyEvent
                            {
                                Key = key,
                                Action = KeyAction.Down,
                                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            };
                            OnKeyEvent?.Invoke(keyEvent);
                            Console.WriteLine($"[KeyboardCapture] Bat phim: {key} DOWN");
                            Debug.WriteLine($"[KeyboardCapture] {key} DOWN");
                        }
                        else if (!isPressed && wasPressedBefore)
                        {
                            // Key UP
                            var keyEvent = new KeyEvent
                            {
                                Key = key,
                                Action = KeyAction.Up,
                                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            };
                            OnKeyEvent?.Invoke(keyEvent);
                            Console.WriteLine($"[KeyboardCapture] Nha phim: {key} UP");
                            Debug.WriteLine($"[KeyboardCapture] {key} UP");
                        }

                        _previousKeyStates[key] = isPressed;
                    }

                    await Task.Delay(10, token); // Poll mỗi 10ms để responsive
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[KeyboardCapture] Error: {ex.Message}");
                }
            }
        }

        private bool IsKeyPressed(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopCapture();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Virtual Key Codes (Windows)
    /// </summary>
    public enum VirtualKey : int
    {
        // Numbers
        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,
        
        // Letters A-Z
        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,
        
        // Function keys
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,
        
        // Numpad
        NumPad0 = 0x60,
        NumPad1 = 0x61,
        NumPad2 = 0x62,
        NumPad3 = 0x63,
        NumPad4 = 0x64,
        NumPad5 = 0x65,
        NumPad6 = 0x66,
        NumPad7 = 0x67,
        NumPad8 = 0x68,
        NumPad9 = 0x69,
        Multiply = 0x6A,
        Add = 0x6B,
        Separator = 0x6C,
        Subtract = 0x6D,
        Decimal = 0x6E,
        Divide = 0x6F,
        
        // Control keys
        Back = 0x08,        // Backspace
        Tab = 0x09,
        Enter = 0x0D,
        Shift = 0x10,
        Ctrl = 0x11,
        Alt = 0x12,
        Pause = 0x13,
        CapsLock = 0x14,
        Escape = 0x1B,
        Space = 0x20,
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        PrintScreen = 0x2C,
        Insert = 0x2D,
        Delete = 0x2E,
        
        // Special keys
        LWin = 0x5B,        // Left Windows (keep for reference, but won't capture)
        RWin = 0x5C,        // Right Windows
        Apps = 0x5D,        // Context menu
        NumLock = 0x90,
        ScrollLock = 0x91,
        
        // Shift variants
        LShift = 0xA0,
        RShift = 0xA1,
        LCtrl = 0xA2,
        RCtrl = 0xA3,
        LAlt = 0xA4,
        RAlt = 0xA5,
        
        // OEM keys (symbols)
        OemSemicolon = 0xBA,    // ; :
        OemPlus = 0xBB,         // = +
        OemComma = 0xBC,        // , <
        OemMinus = 0xBD,        // - _
        OemPeriod = 0xBE,       // . >
        OemQuestion = 0xBF,     // / ?
        OemTilde = 0xC0,        // ` ~
        OemOpenBrackets = 0xDB, // [ {
        OemPipe = 0xDC,         // \ |
        OemCloseBrackets = 0xDD,// ] }
        OemQuotes = 0xDE        // ' "
    }

    public enum KeyAction : byte
    {
        Down = 0,
        Up = 1
    }

    public struct KeyEvent
    {
        public VirtualKey Key;
        public KeyAction Action;
        public long Timestamp;
    }
}
