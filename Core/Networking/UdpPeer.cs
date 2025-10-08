using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Networking;
using RealTimeUdpStream.Core.Util;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Networking
{
    public class UdpPeer : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly PacketBuilder _packetBuilder = new PacketBuilder();
        private readonly PacketParser _packetParser = new PacketParser();
        private readonly NetworkStats _networkStats = new NetworkStats();

        private readonly ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>> _reassemblyBuffers =
            new ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>>();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private int _sequenceNumber = 0;

        public Action<UdpPacket> OnPacketReceived;
        public Action<TelemetrySnapshot> OnTelemetryUpdated;

        public int MaximumTransferUnit { get; } = 1400;

        public UdpPeer(int localPort)
        {
            _udpClient = new UdpClient(localPort);
        }

        // For client mode
        //public UdpPeer(IPEndPoint remoteEndPoint)
        //{
        //    _udpClient = new UdpClient();
        //    _udpClient.Connect(remoteEndPoint);
        //}

        // Phương thức mới để gửi gói tin đến một địa chỉ cụ thể
        public async Task SendToAsync(UdpPacket packet, IPEndPoint remoteEndPoint)
        {
            var payload = packet.Payload ?? Array.Empty<byte>();
            int packetSize = PacketBuilder.HeaderSize + payload.Length;

            var buffer = BytePool.Rent(packetSize);

            try
            {
                _packetBuilder.WriteHeader(packet.Header, buffer);
                payload.CopyTo(buffer.AsSpan(PacketBuilder.HeaderSize));
                _packetBuilder.WriteChecksum(buffer.AsSpan(0, packetSize));

                // Sử dụng phương thức SendAsync với địa chỉ đích
                await _udpClient.SendAsync(buffer, packetSize, remoteEndPoint);

                _networkStats.LogPacketSent(packet.Header.SequenceNumber);
            }
            finally
            {
                BytePool.Return(buffer);
            }
        }

        public async Task StartReceivingAsync()
        {
            var token = _cancellationTokenSource.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var receiveResult = await _udpClient.ReceiveAsync();
                    var buffer = receiveResult.Buffer;
                    var source = receiveResult.RemoteEndPoint;

                    ProcessReceivedPacket(buffer, source);
                }
            }
            catch (OperationCanceledException)
            {
                // Task was canceled
            }
            catch (SocketException)
            {
                // Socket closed
            }
        }

        // Phương thức cũ, dùng khi UdpClient đã Connect
        public async Task SendPacketAsync(UdpPacket packet)
        {
            var payload = packet.Payload ?? Array.Empty<byte>();
            int packetSize = PacketBuilder.HeaderSize + payload.Length;

            var buffer = BytePool.Rent(packetSize);

            try
            {
                _packetBuilder.WriteHeader(packet.Header, buffer);
                payload.CopyTo(buffer.AsSpan(PacketBuilder.HeaderSize));
                _packetBuilder.WriteChecksum(buffer.AsSpan(0, packetSize));

                // Lỗi: Phương thức này chỉ dùng khi đã Connect
                await _udpClient.SendAsync(buffer, packetSize);

                _networkStats.LogPacketSent(packet.Header.SequenceNumber);
            }
            finally
            {
                BytePool.Return(buffer);
            }
        }

        public async Task SendDataAsync(byte[] data, UdpPacketType type, PacketFlags flags = PacketFlags.None)
        {
            int maxPayloadSize = MaximumTransferUnit - PacketBuilder.HeaderSize;
            int totalChunks = (int)Math.Ceiling((double)data.Length / maxPayloadSize);

            var sequenceNumber = (uint)Interlocked.Increment(ref _sequenceNumber);

            for (ushort i = 0; i < totalChunks; i++)
            {
                int chunkOffset = i * maxPayloadSize;
                int chunkSize = Math.Min(maxPayloadSize, data.Length - chunkOffset);

                var chunkPayload = new byte[maxPayloadSize];
                Array.Copy(data, chunkOffset, chunkPayload, 0, chunkSize);

                var header = new UdpPacketHeader
                {
                    Version = 1,
                    PacketType = (byte)type,
                    Flags = (byte)flags,
                    SequenceNumber = sequenceNumber,
                    TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond),
                    TotalChunks = (ushort)totalChunks,
                    ChunkId = i,
                };

                var packet = new UdpPacket(header, chunkPayload);
                // Lỗi: không có địa chỉ đích ở đây
                // await SendPacketAsync(packet);

                // TODO: Cần có địa chỉ đích để gửi
            }
        }

        private void ProcessReceivedPacket(byte[] buffer, IPEndPoint source)
        {
            if (!_packetParser.TryReadHeader(buffer, out var header))
            {
                return;
            }

            if (!_packetParser.IsChecksumValid(buffer))
            {
                return;
            }

            _networkStats.LogPacketReceived();

            var payload = new byte[buffer.Length - PacketParser.HeaderSize];
            Array.Copy(buffer, PacketParser.HeaderSize, payload, 0, payload.Length);

            var packet = new UdpPacket(header, payload) { Source = source };

            switch ((UdpPacketType)header.PacketType)
            {
                case UdpPacketType.Video:
                case UdpPacketType.Audio:
                    HandleDataPacket(packet);
                    break;
                case UdpPacketType.Fec:
                    HandleFecPacket(packet);
                    break;
                case UdpPacketType.Ping:
                    var pongHeader = new UdpPacketHeader
                    {
                        Version = 1,
                        PacketType = (byte)UdpPacketType.Pong,
                        SequenceNumber = header.SequenceNumber,
                        TimestampMs = header.TimestampMs
                    };
                    var pongPacket = new UdpPacket(pongHeader, Array.Empty<byte>());
                    Task.Run(() => SendPacketAsync(pongPacket));
                    break;
                case UdpPacketType.Pong:
                    _networkStats.UpdateRtt((long)header.TimestampMs);
                    break;
            }
        }

        private void HandleDataPacket(UdpPacket packet)
        {
            if (packet.Header.TotalChunks > 1)
            {
                var chunks = _reassemblyBuffers.GetOrAdd(packet.Header.SequenceNumber, key => new ConcurrentDictionary<ushort, UdpPacket>());
                chunks.TryAdd(packet.Header.ChunkId, packet);

                if (chunks.Count == packet.Header.TotalChunks)
                {
                    var sortedChunks = chunks.OrderBy(c => c.Key).Select(c => c.Value).ToList();
                    var fullPayload = new byte[sortedChunks.Sum(c => c.Payload.Length)];
                    int offset = 0;
                    foreach (var chunk in sortedChunks)
                    {
                        Array.Copy(chunk.Payload, 0, fullPayload, offset, chunk.Payload.Length);
                        offset += chunk.Payload.Length;
                    }

                    var fullPacket = new UdpPacket(packet.Header, fullPayload);
                    OnPacketReceived?.Invoke(fullPacket);
                    _reassemblyBuffers.TryRemove(packet.Header.SequenceNumber, out _);
                }
            }
            else
            {
                OnPacketReceived?.Invoke(packet);
            }
        }

        private void HandleFecPacket(UdpPacket fecPacket)
        {
            // TODO: Triển khai logic khôi phục gói tin bằng FEC tại đây.
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _udpClient.Close();
        }

        public void Dispose()
        {
            Stop();
            _udpClient.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}