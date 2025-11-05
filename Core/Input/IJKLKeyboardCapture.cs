using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RealTimeUdpStream.Core.Input
{
    /// <summary>
    /// Capture CHỈ phím IJKL (cho ViGEm controller)
    /// </summary>
    public class IJKLKeyboardCapture : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private readonly Dictionary<VirtualKey, bool> _previousKeyStates;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isCapturing = false;
        private bool _disposed = false;

        public event Action<KeyEvent> OnKeyEvent;

        public IJKLKeyboardCapture()
        {
            // Khởi tạo CHỈ với các phím IJKL
            _previousKeyStates = new Dictionary<VirtualKey, bool>
            {
                { VirtualKey.I, false },
                { VirtualKey.J, false },
                { VirtualKey.K, false },
                { VirtualKey.L, false }
            };
        }

        public void StartCapture()
        {
            if (_isCapturing || _disposed) return;

            _isCapturing = true;
            _cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => CaptureLoop(_cancellationTokenSource.Token));
            Debug.WriteLine("[IJKLKeyboardCapture] Started capturing IJKL keys");
        }

        public void StopCapture()
        {
            if (!_isCapturing) return;

            _cancellationTokenSource?.Cancel();
            _isCapturing = false;
            Debug.WriteLine("[IJKLKeyboardCapture] Stopped capturing IJKL keys");
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
                            Console.WriteLine($"[IJKLKeyboardCapture] Bat phim: {key} DOWN");
                            Debug.WriteLine($"[IJKLKeyboardCapture] {key} DOWN");
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
                            Console.WriteLine($"[IJKLKeyboardCapture] Nha phim: {key} UP");
                            Debug.WriteLine($"[IJKLKeyboardCapture] {key} UP");
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
                    Debug.WriteLine($"[IJKLKeyboardCapture] Error: {ex.Message}");
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
}
