using RealTimeUdpStream.Core.Util;
using System;
using System.Net;

namespace RealTimeUdpStream.Core.Models
{
    // Cấu trúc header UDP chung (21 bytes)
    public struct UdpPacketHeader
    {
        public byte Version;
        public byte PacketType;
        public byte Flags;
        public uint SequenceNumber; // big-endian
        public ulong TimestampMs;    // big-endian
        public ushort Checksum;     // big-endian
        public ushort TotalChunks;  // big-endian
        public ushort ChunkId;      // big-endian
    }

    // Lớp wrapper cho gói tin UDP, chứa header và payload
    // Lớp này giờ đây implement IDisposable để quản lý tài nguyên từ BytePool
    public class UdpPacket : IDisposable
    {
        public UdpPacketHeader Header { get; set; }
        public ArraySegment<byte> Payload { get; }
        public IPEndPoint Source { get; set; }

        /// <summary>
        /// Cờ để xác định xem payload có phải là buffer từ pool hay không.
        /// </summary>
        public bool IsPayloadFromPool { get; set; }

        private bool _disposed = false;

        /// <summary>
        /// Constructor chính, nhận một ArraySegment để tránh copy dữ liệu.
        /// </summary>
        public UdpPacket(UdpPacketHeader header, ArraySegment<byte> payload)
        {
            Header = header;
            Payload = payload;
            IsPayloadFromPool = false; // Mặc định không phải từ pool
        }

        /// <summary>
        /// Constructor tiện ích dùng cho các packet không có payload.
        /// </summary>
        public UdpPacket(UdpPacketType type, uint sequenceNumber)
        {
            Header = new UdpPacketHeader
            {
                Version = 1,
                PacketType = (byte)type,
                SequenceNumber = sequenceNumber,
                TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond)
            };
            Payload = new ArraySegment<byte>(Array.Empty<byte>());
            IsPayloadFromPool = false;
        }

        // CÁC CONSTRUCTOR CŨ NHẬN byte[] ĐÃ BỊ LOẠI BỎ ĐỂ TRÁNH NHẦM LẪN

        /// <summary>
        /// Trả lại buffer của payload về BytePool nếu cần.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (IsPayloadFromPool && Payload.Array != null)
                {
                    BytePool.Return(Payload.Array);
                }
                _disposed = true;
            }
        }
    }

    // Enum và Flags giữ nguyên, không thay đổi
    public enum UdpPacketType : byte
    {
        Input = 0x01, Ping = 0x02, Report = 0x20,
        Video = 0x10, Audio = 0x11, Pong = 0x12, Control = 0x13, Fec = 0x14, Screen = 0x15, Keyboard = 0x16, ViGEm = 0x17
    }

    [Flags]
    public enum PacketFlags : byte
    {
        None = 0, IsKeyframe = 1 << 0, IsPartial = 1 << 2
    }
}