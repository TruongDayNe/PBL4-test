using RealtimeUdpStream.Core.Networking;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RealTimeUdpStream.Core.Models
{
    /// <summary>
    /// Quản lý một nhóm các gói tin dữ liệu và gói tin FEC tương ứng.
    /// </summary>
    public class FecGroup
    {
        // Danh sách các gói tin dữ liệu đã nhận được trong nhóm
        public ConcurrentDictionary<ushort, UdpPacket> DataPackets { get; } = new ConcurrentDictionary<ushort, UdpPacket>();

        // Gói tin FEC đã nhận được
        public UdpPacket FecPacket { get; private set; }

        // Tổng số gói tin dữ liệu mong đợi trong nhóm này
        private readonly int _expectedDataPacketCount;

        // ID của chunk đầu tiên trong nhóm
        private readonly ushort _startChunkId;

        public FecGroup(ushort startChunkId, int expectedDataPacketCount)
        {
            _startChunkId = startChunkId;
            _expectedDataPacketCount = expectedDataPacketCount;
        }

        /// <summary>
        /// Thêm một gói tin (dữ liệu hoặc FEC) vào nhóm.
        /// </summary>
        public void AddPacket(UdpPacket packet)
        {
            if (packet.Header.PacketType == (byte)UdpPacketType.Fec)
            {
                FecPacket = packet;
            }
            else
            {
                DataPackets.TryAdd(packet.Header.ChunkId, packet);
            }
        }

        /// <summary>
        /// Kiểm tra xem có thể phục hồi một gói tin bị mất hay không.
        /// Điều kiện: Đã nhận đủ (tổng số - 1) gói dữ liệu VÀ đã nhận gói FEC.
        /// </summary>
        public bool CanRecover()
        {
            return FecPacket != null && DataPackets.Count == _expectedDataPacketCount - 1;
        }

        /// <summary>
        /// Thực hiện phục hồi gói tin bị mất.
        /// </summary>
        public UdpPacket Recover()
        {
            if (!CanRecover()) return null;

            // Tìm ra ChunkId bị thiếu
            ushort missingChunkId = 0;
            var receivedChunkIds = new HashSet<ushort>(DataPackets.Keys);
            for (ushort i = 0; i < _expectedDataPacketCount; i++)
            {
                ushort currentChunkId = (ushort)(_startChunkId + i);
                if (!receivedChunkIds.Contains(currentChunkId))
                {
                    missingChunkId = currentChunkId;
                    break;
                }
            }

            // Gọi hàm FecXor để tái tạo packet
            var recoveredPacket = FecXor.RecoverPacket(FecPacket, DataPackets.Values);
            if (recoveredPacket != null)
            {
                // Cập nhật lại header cho đúng với packet đã mất
                var newHeader = recoveredPacket.Header;
                newHeader.ChunkId = missingChunkId;
                newHeader.TotalChunks = FecPacket.Header.TotalChunks; // Lấy tổng số chunk từ gói FEC
                newHeader.SequenceNumber = FecPacket.Header.SequenceNumber;
                recoveredPacket.Header = newHeader;
            }

            return recoveredPacket;
        }
    }
}