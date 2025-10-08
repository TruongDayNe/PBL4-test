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
        public ulong TimestampMs;   // big-endian
        public ushort Checksum;     // big-endian
        public ushort TotalChunks;  // big-endian
        public ushort ChunkId;      // big-endian
    }

    // Lớp wrapper cho gói tin UDP, chứa header và payload
    public class UdpPacket
    {
        public UdpPacketHeader Header { get; }
        public byte[] Payload { get; }
        public IPEndPoint Source { get; set; }

        public UdpPacket(UdpPacketHeader header, byte[] payload)
        {
            Header = header;
            Payload = payload;
        }

        public UdpPacket(UdpPacketType type, uint sequenceNumber, ushort totalChunks = 1, ushort chunkId = 0)
        {
            // Constructor tiện ích để tạo nhanh gói tin
            Header = new UdpPacketHeader
            {
                Version = 1, // Phiên bản giao thức
                PacketType = (byte)type,
                SequenceNumber = sequenceNumber,
                TotalChunks = totalChunks,
                ChunkId = chunkId,
                TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond)
            };
            Payload = Array.Empty<byte>();
        }

        // Constructor tiện ích để tạo nhanh gói tin với payload
        public UdpPacket(UdpPacketType type, uint sequenceNumber, byte[] payload, ushort totalChunks = 1, ushort chunkId = 0)
        {
            Header = new UdpPacketHeader
            {
                Version = 1,
                PacketType = (byte)type,
                SequenceNumber = sequenceNumber,
                TotalChunks = totalChunks,
                ChunkId = chunkId,
                TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond)
            };
            Payload = payload;
        }
    }

    // Enum định nghĩa các loại gói tin
    public enum UdpPacketType : byte
    {
        // Client -> Host
        Input = 0x01,
        Ping = 0x02,
        Report = 0x20,

        // Host -> Client
        Video = 0x10,
        Audio = 0x11,
        Pong = 0x12,
        Control = 0x13,
        Fec = 0x14,
        Screen = 0x15
    }

    // Các cờ trạng thái
    [Flags]
    public enum PacketFlags : byte
    {
        None = 0,
        IsKeyframe = 1 << 0, // Dành cho video
        IsLossless = 1 << 1, // Dành cho video
        IsPartial = 1 << 2,  // Dành cho fragment
    }

    // Thông tin telemetry
    public class TelemetrySnapshot
    {
        public TimeSpan Rtt { get; set; }
        public int PacketsSentPerSec { get; set; }
        public int PacketsReceivedPerSec { get; set; }
        public double PacketLossRate { get; set; }
        public long CurrentBitrateKbps { get; set; }
        public double AverageLatencyMs { get; set; }
        public bool IsGpuEnabled { get; set; }
        public string EncoderName { get; set; }
    }
}