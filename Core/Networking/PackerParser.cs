using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;

namespace RealTimeUdpStream.Core.Networking
{
    public class PacketParser
    {
        public const int HeaderSize = 29; // Updated size

        public bool TryReadHeader(ReadOnlySpan<byte> buffer, out UdpPacketHeader header)
        {
            header = new UdpPacketHeader();
            if (buffer.Length < HeaderSize) return false;

            header.Version = buffer[0];
            header.PacketType = buffer[1];
            header.Flags = buffer[2];

            header.SequenceNumber = BigEndian.ReadUInt32(buffer.Slice(3, 4));
            header.TimestampMs = BigEndian.ReadUInt64(buffer.Slice(7, 8));
            header.Checksum = BigEndian.ReadUInt16(buffer.Slice(15, 2));
            header.TotalChunks = BigEndian.ReadUInt16(buffer.Slice(17, 2));
            header.ChunkId = BigEndian.ReadUInt16(buffer.Slice(19, 2));

            // Read new fields
            header.RectX = BigEndian.ReadUInt16(buffer.Slice(21, 2));
            header.RectY = BigEndian.ReadUInt16(buffer.Slice(23, 2));
            header.RectW = BigEndian.ReadUInt16(buffer.Slice(25, 2));
            header.RectH = BigEndian.ReadUInt16(buffer.Slice(27, 2));

            return true;
        }

        public bool IsChecksumValid(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < HeaderSize) return false;

            ushort receivedChecksum = BigEndian.ReadUInt16(buffer.Slice(15, 2));
            Span<byte> tempBuffer = stackalloc byte[buffer.Length];
            buffer.CopyTo(tempBuffer);
            BigEndian.WriteUInt16(tempBuffer.Slice(15, 2), 0);

            ushort calculatedChecksum = Fletcher16(tempBuffer);
            return receivedChecksum == calculatedChecksum;
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