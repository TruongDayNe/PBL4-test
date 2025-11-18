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
            // Khởi tạo CHỈ với các phím WASD
            _previousKeyStates = new Dictionary<VirtualKey, bool>
            {
                { VirtualKey.W, false },
                { VirtualKey.A, false },
                { VirtualKey.S, false },
                { VirtualKey.D, false }
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
