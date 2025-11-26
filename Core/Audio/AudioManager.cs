using RealTimeUdpStream.Core.Models;
using Core.Networking;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RealTimeUdpStream.Core.Audio
{
    /// <summary>
    /// Quản lý tổng thể audio streaming - capture, transmission, và playback
    /// </summary>
    public class AudioManager : IDisposable
    {
        private readonly UdpPeer _udpPeer;
        private readonly AudioConfig _config;
        private readonly bool _isClientMode; // true = CLIENT (có delay), false = HOST (không delay)
        private AudioCapture _audioCapture;
        private AudioPlayback _audioPlayback;
        private bool _disposed = false;
        private bool _isStreaming = false;
        private bool _isReceiving = false;
        private bool _isMuted = false;

        // THÊM: Danh sách các IP bị tắt tiếng
        private readonly HashSet<string> _mutedIps = new HashSet<string>();
        private readonly object _muteLock = new object();


        public void MuteClient(string clientIp, bool isMuted)
        {
            lock (_muteLock)
            {
                if (isMuted) _mutedIps.Add(clientIp);
                else _mutedIps.Remove(clientIp);

                Debug.WriteLine($"[AudioManager] Client {clientIp} muted: {isMuted}");
            }
        }
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                Debug.WriteLine($"[AudioManager] Microphone Muted: {_isMuted}");
            }
        }

        // Target endpoint for audio streaming
        private System.Net.IPEndPoint _targetEndPoint;

        // Packet types for audio
        private const byte AUDIO_PACKET_TYPE = 0x11; // Phải khớp với UdpPacketType.Audio

        public event Action<string> OnStatusChanged;
        public event Action<Exception> OnError;

        public AudioManager(UdpPeer udpPeer, AudioConfig config = null, bool isClientMode = false)
        {
            _udpPeer = udpPeer ?? throw new ArgumentNullException(nameof(udpPeer));
            _config = config ?? AudioConfig.CreateDefault();
            _isClientMode = isClientMode;

            Debug.WriteLine($"=== AudioManager Constructor === Instance={GetHashCode()}");
            Debug.WriteLine($"Mode: {(_isClientMode ? "CLIENT (with delay)" : "HOST (no delay)")}");

            InitializeComponents();
            SubscribeToNetworkEvents();
        }

        public void SetTargetEndPoint(System.Net.IPEndPoint targetEndPoint)
        {
            Debug.WriteLine($"🎯 SetTargetEndPoint CALLED: {targetEndPoint}, _isStreaming={_isStreaming}, Instance={GetHashCode()}");
            _targetEndPoint = targetEndPoint;
            Debug.WriteLine($"🎯 _targetEndPoint NOW SET to: {_targetEndPoint}");
        }

        private void InitializeComponents()
        {
            // CLIENT mode: có delay 3 giây để dễ phân biệt
            // HOST mode: không delay, phát ngay
            _audioPlayback = new AudioPlayback(_config, enableDelay: _isClientMode, delayMs: 3000);
            Debug.WriteLine($"AudioManager initialized - Mode: {(_isClientMode ? "CLIENT (3s delay)" : "HOST (no delay)")}");
        }

        private void SubscribeToNetworkEvents()
        {
            _udpPeer.OnPacketReceived += HandleReceivedPacket;
        }

        public void StartAudioStreaming(AudioInputType inputType = AudioInputType.Microphone)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioManager));
            if (_isStreaming) return;

            try
            {
                Debug.WriteLine($">>> StartAudioStreaming called: InputType={inputType}");
                
                _audioCapture = new AudioCapture(_config, inputType);
                Debug.WriteLine($">>> AudioCapture created");
                
                _audioCapture.OnAudioDataAvailable += SendAudioPacket;
                Debug.WriteLine($">>> OnAudioDataAvailable subscribed");
                
                _audioCapture.StartCapture();
                Debug.WriteLine($">>> StartCapture called");

                _isStreaming = true;
                OnStatusChanged?.Invoke($"Audio streaming started ({inputType})");
                Debug.WriteLine($">>> Audio streaming started successfully");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"Failed to start audio streaming: {ex.Message}");
                throw;
            }
        }

        public void StopAudioStreaming()
        {
            if (!_isStreaming) return;

            try
            {
                _audioCapture?.StopCapture();
                _audioCapture?.Dispose();
                _audioCapture = null;

                _isStreaming = false;
                OnStatusChanged?.Invoke("Audio streaming stopped");
                Debug.WriteLine("Audio streaming stopped");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"Error stopping audio streaming: {ex.Message}");
            }
        }

        public void StartAudioReceiving()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioManager));

            _isReceiving = true;

            // LOG để verify mode
            Debug.WriteLine($"[AudioManager] StartAudioReceiving - Mode: {(_isClientMode ? "CLIENT" : "HOST")}, Delay: {(_isClientMode ? "ENABLED (3s)" : "DISABLED")}");
            Debug.WriteLine($"[AudioManager] AudioPlayback settings - Delay Enabled: {_audioPlayback?.IsDelayEnabled}, Duration: {_audioPlayback?.DelayDurationMs}ms");

            // Start UDP listening
            _ = Task.Run(async () =>
            {
                try
                {
                    await _udpPeer.StartReceivingAsync();
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                }
            });

            OnStatusChanged?.Invoke("Audio receiving started");
            Debug.WriteLine("Audio receiving started");
        }

        public void StopAudioReceiving()
        {
            if (!_isReceiving) return;

            _audioPlayback?.ClearBuffer();
            _isReceiving = false;
            OnStatusChanged?.Invoke("Audio receiving stopped");
            Debug.WriteLine("Audio receiving stopped");
        }

        private void SendAudioPacket(AudioPacket audioPacket)
        {
            if (!_isStreaming || _disposed || _targetEndPoint == null || _isMuted)
            {
                return;
            }

            try
            {
                // Serialize AudioPacket thành byte array
                var packetData = SerializeAudioPacket(audioPacket);

                // Tạo UdpPacket với audio data
                var header = new UdpPacketHeader
                {
                    Version = 1,
                    PacketType = AUDIO_PACKET_TYPE,
                    SequenceNumber = audioPacket.Header.SequenceNumber,
                    TimestampMs = audioPacket.Header.TimestampMs,
                    TotalChunks = 1,
                    ChunkId = 0
                };

                var udpPacket = new UdpPacket(header, new ArraySegment<byte>(packetData));

                // Gửi async đến target endpoint
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _udpPeer.SendToAsync(udpPacket, _targetEndPoint);
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"Error sending audio packet: {ex.Message}");
            }
        }

        private void HandleReceivedPacket(UdpPacket packet)
        {
            if (!_isReceiving || packet.Header.PacketType != AUDIO_PACKET_TYPE) return;

            // Kiểm tra xem IP này có bị mute không
            string sourceIp = packet.Source.Address.ToString();
            lock (_muteLock)
            {
                if (_mutedIps.Contains(sourceIp)) return; // Bỏ qua gói tin nếu bị mute
            }

            try
            {
                var payloadData = new byte[packet.Payload.Count];
                Buffer.BlockCopy(packet.Payload.Array, packet.Payload.Offset, payloadData, 0, packet.Payload.Count);

                var audioPacket = DeserializeAudioPacket(payloadData);
                if (audioPacket != null)
                {
                    _audioPlayback.QueueAudioPacket(audioPacket);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Debug.WriteLine($"Error handling received audio packet: {ex.Message}");
            }
        }

        private byte[] SerializeAudioPacket(AudioPacket packet)
        {
            // Serialize header + audio data
            var headerBytes = packet.Header.ToByteArray();
            var result = new byte[headerBytes.Length + packet.AudioData.Count];

            Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
            Buffer.BlockCopy(packet.AudioData.Array, packet.AudioData.Offset,
                           result, headerBytes.Length, packet.AudioData.Count);

            return result;
        }

        private AudioPacket DeserializeAudioPacket(byte[] data)
        {
            if (data.Length < AudioPacketHeader.HeaderSize) return null;

            try
            {
                var header = AudioPacketHeader.FromByteArray(data);
                var audioDataLength = data.Length - AudioPacketHeader.HeaderSize;

                if (audioDataLength != header.DataLength) return null;

                var audioData = new byte[audioDataLength];
                Buffer.BlockCopy(data, AudioPacketHeader.HeaderSize, audioData, 0, audioDataLength);

                return new AudioPacket(header, new ArraySegment<byte>(audioData));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deserializing audio packet: {ex.Message}");
                return null;
            }
        }

        // Properties
        public bool IsStreaming => _isStreaming;
        public bool IsReceiving => _isReceiving;
        public double PlaybackBufferMs => _audioPlayback?.BufferedDurationMs ?? 0;
        public AudioConfig Config => _config;

        public void Dispose()
        {
            if (_disposed) return;

            StopAudioStreaming();
            StopAudioReceiving();

            _audioPlayback?.Dispose();

            if (_udpPeer != null)
            {
                _udpPeer.OnPacketReceived -= HandleReceivedPacket;
            }

            _disposed = true;
            Debug.WriteLine("AudioManager disposed");
        }
    }
}