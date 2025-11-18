using Core.Networking;
using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RealTimeUdpStream.Core.Input
{
    /// <summary>
    /// Quản lý keyboard input - capture và simulation
    /// </summary>
    public class KeyboardManager : IDisposable
    {
        private readonly UdpPeer _udpPeer;
        private readonly bool _isClientMode; // true = Client (simulate), false = Host (capture)

        private KeyboardCapture _keyboardCapture;
        private KeyboardSimulator _keyboardSimulator;
        private IPEndPoint _targetEndPoint;
        private bool _disposed = false;
        private bool _isCapturing = false;
        private bool _isSimulating = false;

        private const byte KEYBOARD_PACKET_TYPE = 0x16; // Thêm type mới cho keyboard

        public event Action<string> OnStatusChanged;
        public event Action<Exception> OnError;

        public KeyboardManager(UdpPeer udpPeer, bool isClientMode = false)
        {
            _udpPeer = udpPeer ?? throw new ArgumentNullException(nameof(udpPeer));
            _isClientMode = isClientMode;

            Debug.WriteLine($"[KeyboardManager] Initialized - Mode: {(_isClientMode ? "CLIENT (simulate)" : "HOST (capture)")}");
        }

        public void SetTargetEndPoint(IPEndPoint targetEndPoint)
        {
            _targetEndPoint = targetEndPoint;
            Debug.WriteLine($"[KeyboardManager] Target endpoint set: {targetEndPoint}");
        }

        /// <summary>
        /// Bắt đầu capture phím (HOST mode)
        /// </summary>
        public void StartCapture()
        {
            if (_disposed || _isCapturing || _isClientMode) return;

            try
            {
                _keyboardCapture = new KeyboardCapture();
                _keyboardCapture.OnKeyEvent += HandleKeyEvent;
                _keyboardCapture.StartCapture();

                _isCapturing = true;
                OnStatusChanged?.Invoke("Keyboard capture started");
                Debug.WriteLine("[KeyboardManager] Keyboard capture started");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"[KeyboardManager] Failed to start capture: {ex.Message}");
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
                OnStatusChanged?.Invoke("Keyboard capture stopped");
                Debug.WriteLine("[KeyboardManager] Keyboard capture stopped");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"[KeyboardManager] Error stopping capture: {ex.Message}");
            }
        }

        /// <summary>
        /// Bắt đầu nhận và giả lập phím (CLIENT mode)
        /// </summary>
        public void StartSimulation()
        {
            if (_disposed || !_isClientMode) return;

            _isSimulating = true;
            
            // Load key mapping from config
            Console.WriteLine("========================================");
            Console.WriteLine("[KeyboardManager] Loading config...");
            Debug.WriteLine("[KeyboardManager] Loading config...");
            var config = ConfigHelper.GetConfig();
            Console.WriteLine($"[KeyboardManager] Config loaded. Keyboard mappings: {config.KeyboardMapping.Count}");
            Debug.WriteLine($"[KeyboardManager] Config loaded. Keyboard mappings: {config.KeyboardMapping.Count}");
            
            foreach (var kvp in config.KeyboardMapping)
            {
                Console.WriteLine($"[KeyboardManager] Config mapping: {kvp.Key} → {kvp.Value}");
                Debug.WriteLine($"[KeyboardManager] Config mapping: {kvp.Key} → {kvp.Value}");
            }
            
            var keyMapping = ConvertToVirtualKeyMapping(config.KeyboardMapping);
            Console.WriteLine($"[KeyboardManager] Converted {keyMapping.Count} mappings to VirtualKey");
            Debug.WriteLine($"[KeyboardManager] Converted {keyMapping.Count} mappings to VirtualKey");
            
            foreach (var kvp in keyMapping)
            {
                Console.WriteLine($"[KeyboardManager] VirtualKey mapping: {kvp.Key} → {kvp.Value}");
                Debug.WriteLine($"[KeyboardManager] VirtualKey mapping: {kvp.Key} → {kvp.Value}");
            }
            Console.WriteLine("========================================");
            
            _keyboardSimulator = new KeyboardSimulator(keyMapping);

            // Subscribe to network events
            _udpPeer.OnPacketReceived += HandleReceivedPacket;
            Console.WriteLine("[KeyboardManager] Da subscribe vao OnPacketReceived event");

            OnStatusChanged?.Invoke("Keyboard simulation started");
            Debug.WriteLine("[KeyboardManager] Keyboard simulation started");
        }
        
        /// <summary>
        /// Convert string-based mapping to VirtualKey mapping
        /// </summary>
        private Dictionary<VirtualKey, VirtualKey> ConvertToVirtualKeyMapping(Dictionary<string, string> stringMapping)
        {
            var result = new Dictionary<VirtualKey, VirtualKey>();
            
            foreach (var kvp in stringMapping)
            {
                // Skip empty or null mappings
                if (string.IsNullOrWhiteSpace(kvp.Value))
                {
                    Debug.WriteLine($"  {kvp.Key} → (skipped - empty mapping)");
                    continue;
                }
                
                if (TryParseVirtualKey(kvp.Key, out VirtualKey sourceKey) &&
                    TryParseVirtualKey(kvp.Value, out VirtualKey targetKey))
                {
                    result[sourceKey] = targetKey;
                    Debug.WriteLine($"  {kvp.Key} → {kvp.Value}");
                }
                else
                {
                    Debug.WriteLine($"⚠️ Invalid key mapping: {kvp.Key} → {kvp.Value}");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Parse string to VirtualKey enum
        /// </summary>
        private bool TryParseVirtualKey(string keyString, out VirtualKey key)
        {
            // Try direct enum parse
            if (Enum.TryParse(keyString, true, out key))
                return true;
            
            // Handle special cases
            key = (VirtualKey)0;
            return false;
        }

        /// <summary>
        /// Dừng giả lập phím
        /// </summary>
        public void StopSimulation()
        {
            if (!_isSimulating) return;

            _keyboardSimulator?.Dispose();
            _keyboardSimulator = null;

            if (_udpPeer != null)
            {
                _udpPeer.OnPacketReceived -= HandleReceivedPacket;
            }

            _isSimulating = false;
            OnStatusChanged?.Invoke("Keyboard simulation stopped");
            Debug.WriteLine("[KeyboardManager] Keyboard simulation stopped");
        }

        /// <summary>
        /// Xử lý key event từ capture và gửi qua mạng
        /// </summary>
        private void HandleKeyEvent(KeyEvent keyEvent)
        {
            if (!_isCapturing || _disposed || _targetEndPoint == null) return;

            try
            {
                Console.WriteLine($"[KeyboardManager] Dang gui phim {keyEvent.Key} {keyEvent.Action} toi {_targetEndPoint}");
                
                // Serialize KeyEvent
                byte[] data = SerializeKeyEvent(keyEvent);

                // Tạo UDP packet
                var header = new UdpPacketHeader
                {
                    Version = 1,
                    PacketType = KEYBOARD_PACKET_TYPE,
                    SequenceNumber = (uint)keyEvent.Timestamp,
                    TimestampMs = (ulong)keyEvent.Timestamp,
                    TotalChunks = 1,
                    ChunkId = 0
                };

                var udpPacket = new UdpPacket(header, new ArraySegment<byte>(data));

                // Gửi async
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _udpPeer.SendToAsync(udpPacket, _targetEndPoint);
                        Console.WriteLine($"[KeyboardManager] Da gui thanh cong phim {keyEvent.Key} {keyEvent.Action}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[KeyboardManager] Loi khi gui: {ex.Message}");
                        OnError?.Invoke(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeyboardManager] Loi khi gui: {ex.Message}");
                OnError?.Invoke(ex);
                Debug.WriteLine($"[KeyboardManager] Error sending key event: {ex.Message}");
            }
        }

        /// <summary>
        /// Xử lý packet nhận được và giả lập phím
        /// </summary>
        private void HandleReceivedPacket(UdpPacket packet)
        {
            Console.WriteLine($"[KeyboardManager] HandleReceivedPacket called - Type: 0x{packet.Header.PacketType:X2}, isSimulating: {_isSimulating}");
            
            if (!_isSimulating)
            {
                Console.WriteLine("[KeyboardManager] KHONG SIMULATE - Bo qua packet");
                return;
            }
            
            if (packet.Header.PacketType != KEYBOARD_PACKET_TYPE)
            {
                Console.WriteLine($"[KeyboardManager] KHONG PHAI KEYBOARD PACKET (Expected: 0x{KEYBOARD_PACKET_TYPE:X2}) - Bo qua");
                return;
            }

            try
            {
                Console.WriteLine($"[KeyboardManager] Nhan duoc goi phim (Type: 0x{packet.Header.PacketType:X2})");
                
                var payloadData = new byte[packet.Payload.Count];
                Buffer.BlockCopy(packet.Payload.Array, packet.Payload.Offset, payloadData, 0, packet.Payload.Count);

                var keyEvent = DeserializeKeyEvent(payloadData);
                Console.WriteLine($"[KeyboardManager] Giai ma: {keyEvent.Key} {keyEvent.Action}");
                
                _keyboardSimulator?.SimulateKeyEvent(keyEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeyboardManager] Loi khi nhan: {ex.Message}");
                OnError?.Invoke(ex);
                Debug.WriteLine($"[KeyboardManager] Error handling received packet: {ex.Message}");
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
            Debug.WriteLine("[KeyboardManager] Disposed");
        }
    }
}
