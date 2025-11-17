using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;

namespace RealTimeUdpStream.Core.Networking
{
    public class PacketBuilder
    {
        // Size cũ 21 + 8 bytes mới (X, Y, W, H) = 29
        public const int HeaderSize = 29;

        public void WriteHeader(UdpPacketHeader header, Span<byte> buffer)
        {
            if (buffer.Length < HeaderSize)
                throw new ArgumentException("Buffer is too small for header.");

            buffer[0] = header.Version;
            buffer[1] = header.PacketType;
            buffer[2] = header.Flags;

            BigEndian.WriteUInt32(buffer.Slice(3, 4), header.SequenceNumber);
            BigEndian.WriteUInt64(buffer.Slice(7, 8), header.TimestampMs);
            BigEndian.WriteUInt16(buffer.Slice(15, 2), header.Checksum);
            BigEndian.WriteUInt16(buffer.Slice(17, 2), header.TotalChunks);
            BigEndian.WriteUInt16(buffer.Slice(19, 2), header.ChunkId);

            // Write new fields
            BigEndian.WriteUInt16(buffer.Slice(21, 2), header.RectX);
            BigEndian.WriteUInt16(buffer.Slice(23, 2), header.RectY);
            BigEndian.WriteUInt16(buffer.Slice(25, 2), header.RectW);
            BigEndian.WriteUInt16(buffer.Slice(27, 2), header.RectH);
        }

        public void WriteChecksum(Span<byte> buffer)
        {
            if (buffer.Length < HeaderSize)
                throw new ArgumentException("Buffer is too small for header.");

            BigEndian.WriteUInt16(buffer.Slice(15, 2), 0);
            ushort checksum = Fletcher16(buffer);
            BigEndian.WriteUInt16(buffer.Slice(15, 2), checksum);
        }

        private static ushort Fletcher16(ReadOnlySpan<byte> data)
        {
            ushort sum1 = 0;
            ushort sum2 = 0;
            foreach (var b in data)
            {
                sum1 = (ushort)((sum1 + b) % 255);
                sum2 = (ushort)((sum2 + sum1) % 255);
            }
            return (ushort)((sum2 << 8) | sum1);
        }
    }
}