using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;
using System.Security.Cryptography;

namespace RealTimeUdpStream.Core.Networking
{
    public class PacketBuilder
    {
        public const int HeaderSize = 21;

        /// <summary>
        /// Ghi header gói tin vào mảng byte.
        /// </summary>
        public void WriteHeader(UdpPacketHeader header, Span<byte> buffer)
        {
            if (buffer.Length < HeaderSize)
            {
                throw new ArgumentException("Buffer is too small for header.");
            }

            buffer[0] = header.Version;
            buffer[1] = header.PacketType;
            buffer[2] = header.Flags;

            BigEndian.WriteUInt32(buffer.Slice(3, 4), header.SequenceNumber);
            BigEndian.WriteUInt64(buffer.Slice(7, 8), header.TimestampMs);
            BigEndian.WriteUInt16(buffer.Slice(15, 2), header.Checksum);
            BigEndian.WriteUInt16(buffer.Slice(17, 2), header.TotalChunks);
            BigEndian.WriteUInt16(buffer.Slice(19, 2), header.ChunkId);
        }

        /// <summary>
        /// Tính toán và ghi checksum vào gói tin.
        /// </summary>
        public void WriteChecksum(Span<byte> buffer)
        {
            if (buffer.Length < HeaderSize)
            {
                throw new ArgumentException("Buffer is too small for header.");
            }

            // Đặt checksum tạm thời về 0 để tính toán
            BigEndian.WriteUInt16(buffer.Slice(15, 2), 0);

            ushort checksum = Fletcher16(buffer);

            // Ghi checksum đã tính toán vào vị trí 15
            BigEndian.WriteUInt16(buffer.Slice(15, 2), checksum);
        }

        /// <summary>
        /// Tính toán Fletcher-16 Checksum.
        /// </summary>
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