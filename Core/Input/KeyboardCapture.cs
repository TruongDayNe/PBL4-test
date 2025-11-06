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
        W = 0x57,
        A = 0x41,
        S = 0x53,
        D = 0x44,
        Space = 0x20,
        Shift = 0x10,
        Ctrl = 0x11,
        T = 0x54,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        O = 0x4F
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
