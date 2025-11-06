using Core.Networking;
using RealTimeUdpStream.Core.Input;
using RealTimeUdpStream.Core.Models;
using System;
using System.Diagnostics;
using System.Net;

namespace RealTimeUdpStream.Core.ViGEm
{
    /// <summary>
    /// Quản lý ViGEm controller - capture IJKL keys và simulate joystick
    /// </summary>
    public class ViGEmManager : IDisposable
    {
        private readonly UdpPeer _udpPeer;
        private readonly bool _isClientMode; // true = Client (capture), false = Host (simulate)

        private IJKLKeyboardCapture _keyboardCapture; // Dùng IJKLKeyboardCapture thay vì KeyboardCapture
        private ViGEmController _vigemController;
        private IPEndPoint _targetEndPoint;
        private bool _disposed = false;
        private bool _isCapturing = false;
        private bool _isSimulating = false;

        private const byte VIGEM_PACKET_TYPE = 0x17; // Packet type cho ViGEm controller

        public event Action<string> OnStatusChanged;
        public event Action<Exception> OnError;

        public ViGEmManager(UdpPeer udpPeer, bool isClientMode = false)
        {
            _udpPeer = udpPeer ?? throw new ArgumentNullException(nameof(udpPeer));
            _isClientMode = isClientMode;

            Debug.WriteLine($"[ViGEmManager] Initialized - Mode: {(_isClientMode ? "CLIENT (capture)" : "HOST (simulate)")}");
        }

        public void SetTargetEndPoint(IPEndPoint targetEndPoint)
        {
            _targetEndPoint = targetEndPoint;
            Debug.WriteLine($"[ViGEmManager] Target endpoint set: {targetEndPoint}");
        }

        /// <summary>
        /// Bắt đầu capture phím IJKL (CLIENT mode)
        /// </summary>
        public void StartCapture()
        {
            if (_disposed || _isCapturing || !_isClientMode) return;

            try
            {
                _keyboardCapture = new IJKLKeyboardCapture(); // Dùng IJKLKeyboardCapture - chỉ capture IJKL
                _keyboardCapture.OnKeyEvent += HandleKeyEvent;
                _keyboardCapture.StartCapture();

                _isCapturing = true;
                OnStatusChanged?.Invoke("ViGEm capture started (IJKL only)");
                Console.WriteLine("[ViGEmManager] ViGEm capture started - CHỈ capture phím IJKL");
                Debug.WriteLine("[ViGEmManager] ViGEm capture started");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"[ViGEmManager] Failed to start capture: {ex.Message}");
            }
        }

        /// <summary>
        /// Dừng capture phím
        /// </summary>
        public void StopCapture()
        {
            if (!_isCapturing) return;

            try
            {
                _keyboardCapture?.StopCapture();
                _keyboardCapture?.Dispose();
                _keyboardCapture = null;

                _isCapturing = false;
                OnStatusChanged?.Invoke("ViGEm capture stopped");
                Debug.WriteLine("[ViGEmManager] ViGEm capture stopped");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"[ViGEmManager] Error stopping capture: {ex.Message}");
            }
        }

        /// <summary>
        /// Bắt đầu nhận và giả lập controller (HOST mode)
        /// </summary>
        public void StartSimulation()
        {
            Console.WriteLine($"[ViGEmManager] StartSimulation called - isClientMode: {_isClientMode}, disposed: {_disposed}, isSimulating: {_isSimulating}");
            
            if (_disposed || _isSimulating || _isClientMode) 
            {
                Console.WriteLine($"[ViGEmManager] StartSimulation SKIPPED - returning early");
                return; // HOST mode = isClientMode FALSE
            }

            try
            {
                Console.WriteLine("[ViGEmManager] Creating ViGEmController...");
                _vigemController = new ViGEmController(); // Constructor đã khởi tạo controller
                Console.WriteLine("[ViGEmManager] ViGEmController created successfully!");

                // Subscribe to network events
                _udpPeer.OnPacketReceived += HandleReceivedPacket;

                _isSimulating = true;
                OnStatusChanged?.Invoke("ViGEm controller simulation started");
                Console.WriteLine("[ViGEmManager] ViGEm controller simulation started - listening for packets");
                Debug.WriteLine("[ViGEmManager] ViGEm controller simulation started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ViGEmManager] FAILED to start simulation: {ex.Message}");
                Console.WriteLine($"[ViGEmManager] Stack trace: {ex.StackTrace}");
                OnError?.Invoke(ex);
                Debug.WriteLine($"[ViGEmManager] Failed to start simulation: {ex.Message}");
            }
        }

        /// <summary>
        /// Dừng giả lập controller
        /// </summary>
        public void StopSimulation()
        {
            if (!_isSimulating) return;

            try
            {
                _vigemController?.Dispose();
                _vigemController = null;

                if (_udpPeer != null)
                {
                    _udpPeer.OnPacketReceived -= HandleReceivedPacket;
                }

                _isSimulating = false;
                OnStatusChanged?.Invoke("ViGEm controller simulation stopped");
                Debug.WriteLine("[ViGEmManager] ViGEm controller simulation stopped");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"[ViGEmManager] Error stopping simulation: {ex.Message}");
            }
        }

        /// <summary>
        /// Xử lý key event từ capture và gửi qua mạng
        /// </summary>
        private void HandleKeyEvent(KeyEvent keyEvent)
        {
            if (!_isCapturing || _disposed || _targetEndPoint == null) return;

            // Chỉ xử lý phím IJKLO
            if (keyEvent.Key != VirtualKey.I && 
                keyEvent.Key != VirtualKey.J && 
                keyEvent.Key != VirtualKey.K && 
                keyEvent.Key != VirtualKey.L &&
                keyEvent.Key != VirtualKey.O)
            {
                return;
            }

            try
            {
                Console.WriteLine($"[ViGEmManager] Sending key {keyEvent.Key} {keyEvent.Action} to {_targetEndPoint}");

                // Serialize KeyEvent
                byte[] data = SerializeKeyEvent(keyEvent);

                // Tạo UDP packet
                var header = new UdpPacketHeader
                {
                    Version = 1,
                    PacketType = VIGEM_PACKET_TYPE,
                    SequenceNumber = (uint)keyEvent.Timestamp,
                    TimestampMs = (ulong)keyEvent.Timestamp,
                    TotalChunks = 1,
                    ChunkId = 0
                };

                var udpPacket = new UdpPacket(header, new ArraySegment<byte>(data));

                // Gửi async
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await _udpPeer.SendToAsync(udpPacket, _targetEndPoint);
                        Console.WriteLine($"[ViGEmManager] Successfully sent key {keyEvent.Key} {keyEvent.Action}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ViGEmManager] Error sending: {ex.Message}");
                        OnError?.Invoke(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ViGEmManager] Error sending: {ex.Message}");
                OnError?.Invoke(ex);
                Debug.WriteLine($"[ViGEmManager] Error sending key event: {ex.Message}");
            }
        }

        /// <summary>
        /// Xử lý packet nhận được và giả lập controller
        /// </summary>
        private void HandleReceivedPacket(UdpPacket packet)
        {
            Console.WriteLine($"[ViGEmManager] HandleReceivedPacket called - Type: 0x{packet.Header.PacketType:X2}, isSimulating: {_isSimulating}, Expected: 0x{VIGEM_PACKET_TYPE:X2}");
            
            if (!_isSimulating)
            {
                Console.WriteLine("[ViGEmManager] NOT simulating - packet ignored");
                return;
            }
            
            if (packet.Header.PacketType != VIGEM_PACKET_TYPE)
            {
                Console.WriteLine($"[ViGEmManager] Wrong packet type (got 0x{packet.Header.PacketType:X2}, expected 0x{VIGEM_PACKET_TYPE:X2}) - packet ignored");
                return;
            }

            try
            {
                Console.WriteLine($"[ViGEmManager] ✅ Received ViGEm packet (Type: 0x{packet.Header.PacketType:X2})");

                var payloadData = new byte[packet.Payload.Count];
                Buffer.BlockCopy(packet.Payload.Array, packet.Payload.Offset, payloadData, 0, packet.Payload.Count);

                var keyEvent = DeserializeKeyEvent(payloadData);
                Console.WriteLine($"[ViGEmManager] Decoded: {keyEvent.Key} {keyEvent.Action}");

                // Update controller state based on key
                UpdateControllerState(keyEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ViGEmManager] Error receiving: {ex.Message}");
                OnError?.Invoke(ex);
                Debug.WriteLine($"[ViGEmManager] Error handling received packet: {ex.Message}");
            }
        }

        /// <summary>
        /// Update controller joystick based on key event
        /// </summary>
        private void UpdateControllerState(KeyEvent keyEvent)
        {
            Console.WriteLine($"[ViGEmManager] UpdateControllerState called - Key: {keyEvent.Key}, Action: {keyEvent.Action}");
            
            if (_vigemController == null) 
            {
                Console.WriteLine("[ViGEmManager] ❌ Controller is NULL - cannot update state!");
                return;
            }

            bool isPressed = (keyEvent.Action == KeyAction.Down);
            Console.WriteLine($"[ViGEmManager] isPressed: {isPressed}");

            switch (keyEvent.Key)
            {
                case VirtualKey.I:
                    _vigemController.SetIPressedState(isPressed);
                    Console.WriteLine($"[ViGEmManager] I key {(isPressed ? "PRESSED" : "RELEASED")} - Joystick UP");
                    break;
                case VirtualKey.K:
                    _vigemController.SetKPressedState(isPressed);
                    Console.WriteLine($"[ViGEmManager] K key {(isPressed ? "PRESSED" : "RELEASED")} - Joystick DOWN");
                    break;
                case VirtualKey.J:
                    _vigemController.SetJPressedState(isPressed);
                    Console.WriteLine($"[ViGEmManager] J key {(isPressed ? "PRESSED" : "RELEASED")} - Joystick LEFT");
                    break;
                case VirtualKey.L:
                    _vigemController.SetLPressedState(isPressed);
                    Console.WriteLine($"[ViGEmManager] L key {(isPressed ? "PRESSED" : "RELEASED")} - Joystick RIGHT");
                    break;
                case VirtualKey.O:
                    _vigemController.SetOPressedState(isPressed);
                    Console.WriteLine($"[ViGEmManager] O key {(isPressed ? "PRESSED" : "RELEASED")} - Button A");
                    break;
            }
        }

        /// <summary>
        /// Serialize KeyEvent thành byte array (3 bytes)
        /// </summary>
        private byte[] SerializeKeyEvent(KeyEvent keyEvent)
        {
            // Format: [Key: 1 byte][Action: 1 byte][Reserved: 1 byte]
            return new byte[]
            {
                (byte)keyEvent.Key,
                (byte)keyEvent.Action,
                0 // Reserved
            };
        }

        /// <summary>
        /// Deserialize byte array thành KeyEvent
        /// </summary>
        private KeyEvent DeserializeKeyEvent(byte[] data)
        {
            if (data.Length < 2) throw new ArgumentException("Invalid key event data");

            return new KeyEvent
            {
                Key = (VirtualKey)data[0],
                Action = (KeyAction)data[1],
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public bool IsCapturing => _isCapturing;
        public bool IsSimulating => _isSimulating;

        public void Dispose()
        {
            if (_disposed) return;

            StopCapture();
            StopSimulation();

            _disposed = true;
            Debug.WriteLine("[ViGEmManager] Disposed");
        }
    }
}
