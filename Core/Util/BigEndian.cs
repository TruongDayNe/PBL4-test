using System;
using System.Buffers.Binary;

namespace RealTimeUdpStream.Core.Util
{
    public static class BigEndian
    {
        // Đọc giá trị từ một Span<byte>
        public static ushort ReadUInt16(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt16BigEndian(buffer);
        public static uint ReadUInt32(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt32BigEndian(buffer);
        public static ulong ReadUInt64(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt64BigEndian(buffer);

        // Ghi giá trị vào một Span<byte>
        public static void WriteUInt16(Span<byte> buffer, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        public static void WriteUInt32(Span<byte> buffer, uint value) => BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        public static void WriteUInt64(Span<byte> buffer, ulong value) => BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
    }
}