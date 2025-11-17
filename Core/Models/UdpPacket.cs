using RealTimeUdpStream.Core.Util;
using System;
using System.Net;

namespace RealTimeUdpStream.Core.Models
{
    // Cập nhật Header: Thêm tọa độ và kích thước (Total: 29 bytes)
    public struct UdpPacketHeader
    {
        public byte Version;
        public byte PacketType;
        public byte Flags;          // Bit 0: IsKeyFrame
        public uint SequenceNumber; // big-endian
        public ulong TimestampMs;   // big-endian
        public ushort Checksum;     // big-endian
        public ushort TotalChunks;  // big-endian
        public ushort ChunkId;      // big-endian

        // --- NEW FIELDS FOR PARTIAL UPDATES ---
        public ushort RectX;        // big-endian
        public ushort RectY;        // big-endian
        public ushort RectW;        // big-endian
        public ushort RectH;        // big-endian
    }

    public class UdpPacket : IDisposable
    {
        public UdpPacketHeader Header { get; set; }
        public ArraySegment<byte> Payload { get; }
        public IPEndPoint Source { get; set; }
        public bool IsPayloadFromPool { get; set; }

        private bool _disposed = false;

        public UdpPacket(UdpPacketHeader header, ArraySegment<byte> payload)
        {
            Header = header;
            Payload = payload;
            IsPayloadFromPool = false;
        }

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

    public enum UdpPacketType : byte
    {
        Input = 0x01, Ping = 0x02, Report = 0x20,
        Video = 0x10, Audio = 0x11, Pong = 0x12, Control = 0x13, Fec = 0x14, Screen = 0x15, Keyboard = 0x16, ViGEm = 0x17,
        Disconnect = 0x18, Kick = 0x19
    }

    [Flags]
    public enum PacketFlags : byte
    {
        None = 0,
        IsKeyframe = 1 << 0, // Frame đầy đủ
        IsPartial = 1 << 2
    }
}