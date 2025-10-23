using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Networking;
using RealTimeUdpStream.Core.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Networking
{
    public class UdpPeer : IDisposable
    {
        // Core components
        private readonly UdpClient _udpClient;
        private readonly PacketBuilder _packetBuilder = new PacketBuilder();
        private readonly PacketParser _packetParser = new PacketParser();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // Buffers for packet processing
        private readonly ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>> _reassemblyBuffers = new ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>>();
        private readonly ConcurrentDictionary<uint, ConcurrentDictionary<ushort, FecGroup>> _fecBuffers = new ConcurrentDictionary<uint, ConcurrentDictionary<ushort, FecGroup>>();

        // Stats and Events
        private readonly NetworkStats _networkStats = new NetworkStats();
        public NetworkStats Stats => _networkStats;
        public Action<UdpPacket> OnPacketReceived;
        public int MaximumTransferUnit { get; } = 1400; // Typical MTU for Ethernet

        public UdpPeer(int localPort)
        {
            _udpClient = new UdpClient(localPort);
        }

        /// <summary>
        /// Gửi một gói tin UDP đến địa chỉ cụ thể một cách hiệu quả về bộ nhớ.
        /// </summary>
        public async Task SendToAsync(UdpPacket packet, IPEndPoint remoteEndPoint)
        {
            var payload = packet.Payload;
            int packetSize = PacketBuilder.HeaderSize + payload.Count;
            var buffer = BytePool.Rent(packetSize);
            try
            {
                // Ghi header và payload vào buffer đã thuê
                _packetBuilder.WriteHeader(packet.Header, buffer);
                Buffer.BlockCopy(payload.Array, payload.Offset, buffer, PacketBuilder.HeaderSize, payload.Count);

                // Tính và ghi checksum
                _packetBuilder.WriteChecksum(buffer.AsSpan(0, packetSize));

                await _udpClient.SendAsync(buffer, packetSize, remoteEndPoint);
                _networkStats.LogPacketSent(packet.Header.SequenceNumber, packetSize);

            }
            finally
            {
                // Trả buffer về pool sau khi gửi xong
                BytePool.Return(buffer);
            }
        }

        /// <summary>
        /// Bắt đầu vòng lặp nhận gói tin trên một luồng nền.
        /// </summary>
        public async Task StartReceivingAsync()
        {
            var token = _cancellationTokenSource.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var receiveResult = await _udpClient.ReceiveAsync();
                    ProcessReceivedPacket(receiveResult.Buffer, receiveResult.RemoteEndPoint);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is SocketException)
            {
                // Bỏ qua lỗi khi đóng socket hoặc hủy task, đây là hành vi mong muốn khi dừng lại.
            }
        }

        /// <summary>
        /// Xử lý một buffer nhận được, parse nó thành packet và định tuyến đến handler thích hợp.
        /// </summary>
        private void ProcessReceivedPacket(byte[] buffer, IPEndPoint source)
        {
            if (!_packetParser.TryReadHeader(buffer, out var header) || !_packetParser.IsChecksumValid(buffer))
            {
                return; // Bỏ qua gói tin hỏng hoặc không hợp lệ
            }

            _networkStats.LogPacketReceived(buffer.Length);

            // Tối ưu: Tạo một packet "view" trỏ thẳng vào buffer nhận được, không copy dữ liệu.
            var payloadSegment = new ArraySegment<byte>(buffer, PacketParser.HeaderSize, buffer.Length - PacketParser.HeaderSize);
            var packet = new UdpPacket(header, payloadSegment) { Source = source };

            switch ((UdpPacketType)header.PacketType)
            {
                case UdpPacketType.Video:
                case UdpPacketType.Audio:
                    AddToFecGroup(packet); // Luôn thêm packet vào nhóm FEC để có thể dùng để phục hồi gói khác
                    HandleDataPacket(packet);
                    break;
                case UdpPacketType.Fec:
                    HandleFecPacket(packet);
                    break;
                    // Các loại packet khác có thể được xử lý ở đây
            }
        }

        /// <summary>
        /// Xử lý các gói tin dữ liệu (video/audio), lưu trữ chúng để ghép lại sau.
        /// </summary>
        private void HandleDataPacket(UdpPacket packet)
        {
            if (packet.Header.TotalChunks <= 1)
            {
                OnPacketReceived?.Invoke(packet); // Gửi đi ngay nếu packet không bị chia nhỏ
                return;
            }

            var chunks = _reassemblyBuffers.GetOrAdd(packet.Header.SequenceNumber, _ => new ConcurrentDictionary<ushort, UdpPacket>());

            // Tạo bản sao của payload vì packet gốc sẽ bị hủy sau khi ra khỏi ProcessReceivedPacket
            var payloadCopy = new byte[packet.Payload.Count];
            Buffer.BlockCopy(packet.Payload.Array, packet.Payload.Offset, payloadCopy, 0, payloadCopy.Length);
            var persistentChunk = new UdpPacket(packet.Header, new ArraySegment<byte>(payloadCopy));

            chunks.TryAdd(packet.Header.ChunkId, persistentChunk);

            // Nếu đã nhận đủ các chunk của một frame
            if (chunks.Count == packet.Header.TotalChunks)
            {
                if (_reassemblyBuffers.TryRemove(packet.Header.SequenceNumber, out var completedChunks))
                {
                    AssembleAndDispatchFrame(completedChunks.Values);
                }
            }
        }

        /// <summary>
        /// Xử lý gói tin FEC, lưu trữ và kiểm tra khả năng phục hồi.
        /// </summary>
        private void HandleFecPacket(UdpPacket fecPacket)
        {
            const int FEC_GROUP_SIZE = 10;
            var sequence = fecPacket.Header.SequenceNumber;
            ushort groupStartChunkId = fecPacket.Header.ChunkId; // Theo quy ước từ ScreenSender

            var sequenceFecGroups = _fecBuffers.GetOrAdd(sequence, _ => new ConcurrentDictionary<ushort, FecGroup>());
            var fecGroup = sequenceFecGroups.GetOrAdd(groupStartChunkId, _ => new FecGroup(groupStartChunkId, FEC_GROUP_SIZE));

            var payloadCopy = new byte[fecPacket.Payload.Count];
            Buffer.BlockCopy(fecPacket.Payload.Array, fecPacket.Payload.Offset, payloadCopy, 0, payloadCopy.Length);
            var persistentFecPacket = new UdpPacket(fecPacket.Header, new ArraySegment<byte>(payloadCopy));

            fecGroup.AddPacket(persistentFecPacket);

            // Nếu có thể phục hồi, thực hiện ngay
            if (fecGroup.CanRecover())
            {
                using (var recoveredPacket = fecGroup.Recover())
                {
                    if (recoveredPacket != null)
                    {
                        // Đưa gói tin đã phục hồi vào lại pipeline xử lý
                        HandleDataPacket(recoveredPacket);
                    }
                }
            }
        }

        /// <summary>
        /// Thêm một bản sao của packet dữ liệu vào nhóm FEC tương ứng.
        /// </summary>
        private void AddToFecGroup(UdpPacket packet)
        {
            if (packet.Header.TotalChunks <= 1) return; // Không cần FEC cho packet đơn lẻ

            const int FEC_GROUP_SIZE = 10;
            var sequence = packet.Header.SequenceNumber;
            var chunkId = packet.Header.ChunkId;
            ushort groupStartChunkId = (ushort)(chunkId / FEC_GROUP_SIZE * FEC_GROUP_SIZE);

            var sequenceFecGroups = _fecBuffers.GetOrAdd(sequence, _ => new ConcurrentDictionary<ushort, FecGroup>());
            var fecGroup = sequenceFecGroups.GetOrAdd(groupStartChunkId, _ => new FecGroup(groupStartChunkId, FEC_GROUP_SIZE));

            var payloadCopy = new byte[packet.Payload.Count];
            Buffer.BlockCopy(packet.Payload.Array, packet.Payload.Offset, payloadCopy, 0, payloadCopy.Length);
            var persistentPacket = new UdpPacket(packet.Header, new ArraySegment<byte>(payloadCopy));

            fecGroup.AddPacket(persistentPacket);
        }

        /// <summary>
        /// Sắp xếp, ghép các chunk thành một frame hoàn chỉnh và gửi đi qua event OnPacketReceived.
        /// </summary>
        private void AssembleAndDispatchFrame(ICollection<UdpPacket> chunks)
        {
            var sortedChunks = chunks.OrderBy(c => c.Header.ChunkId).ToList();
            int fullPayloadLength = sortedChunks.Sum(c => c.Payload.Count);
            if (fullPayloadLength == 0) return;

            var fullPayloadBuffer = BytePool.Rent(fullPayloadLength);
            try
            {
                int offset = 0;
                foreach (var chunk in sortedChunks)
                {
                    Buffer.BlockCopy(chunk.Payload.Array, chunk.Payload.Offset, fullPayloadBuffer, offset, chunk.Payload.Count);
                    offset += chunk.Payload.Count;
                }

                var header = sortedChunks.First().Header;
                var fullSegment = new ArraySegment<byte>(fullPayloadBuffer, 0, fullPayloadLength);

                // Gói packet cuối cùng trong 'using' để đảm bảo buffer được trả về pool
                using (var fullPacket = new UdpPacket(header, fullSegment) { IsPayloadFromPool = true })
                {
                    OnPacketReceived?.Invoke(fullPacket);
                }
            }
            catch
            {
                BytePool.Return(fullPayloadBuffer); // Đảm bảo trả buffer nếu có lỗi
                throw;
            }
            finally
            {
                // Dọn dẹp FEC buffer và các chunk đã copy
                if (chunks.Any())
                {
                    var sequence = chunks.First().Header.SequenceNumber;
                    _fecBuffers.TryRemove(sequence, out _);
                }
                foreach (var chunk in chunks) chunk.Dispose();
            }
        }

        /// <summary>
        /// Dừng vòng lặp nhận và đóng UdpClient.
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _udpClient.Close();
            _cancellationTokenSource.Dispose();
        }
    }
}