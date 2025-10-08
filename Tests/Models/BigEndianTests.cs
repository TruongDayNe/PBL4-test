using RealTimeUdpStream.Core.Util;
using System.Buffers;
using System.Buffers.Binary;
using Xunit;

public class BigEndianTests
{
    [Fact]
    public void WriteUInt16_CorrectlyWritesBytes()
    {
        ushort value = 0xABCD;
        Span<byte> buffer = stackalloc byte[2];
        BigEndian.WriteUInt16(buffer, value);
        Assert.Equal(0xAB, buffer[0]);
        Assert.Equal(0xCD, buffer[1]);
    }

    [Fact]
    public void ReadUInt16_CorrectlyReadsBytes()
    {
        byte[] data = { 0xAB, 0xCD };
        ushort value = BigEndian.ReadUInt16(data);
        Assert.Equal(0xABCD, value);
    }

    [Fact]
    public void WriteUInt32_CorrectlyWritesBytes()
    {
        uint value = 0x12345678;
        Span<byte> buffer = stackalloc byte[4];
        BigEndian.WriteUInt32(buffer, value);
        Assert.Equal(0x12, buffer[0]);
        Assert.Equal(0x34, buffer[1]);
        Assert.Equal(0x56, buffer[2]);
        Assert.Equal(0x78, buffer[3]);
    }

    // Các bài test tương tự cho ReadUInt32, WriteUInt64, ReadUInt64
    [Fact]
    public void ReadUInt32_CorrectlyReadsBytes()
    {
        byte[] data = { 0x12, 0x34, 0x56, 0x78 };
        uint value = BigEndian.ReadUInt32(data);
        Assert.Equal((double)0x12345678, value);
    }
    [Fact]
    public void WriteUInt64_CorrectlyWritesBytes()
    {
        ulong value = 0x1234567890ABCDEF;
        Span<byte> buffer = stackalloc byte[8];
        BigEndian.WriteUInt64(buffer, value);
        Assert.Equal(0x12, buffer[0]);
        Assert.Equal(0x34, buffer[1]);
        Assert.Equal(0x56, buffer[2]);
        Assert.Equal(0x78, buffer[3]);
        Assert.Equal(0x90, buffer[4]);
        Assert.Equal(0xAB, buffer[5]);
        Assert.Equal(0xCD, buffer[6]);
        Assert.Equal(0xEF, buffer[7]);
    }
    [Fact]
    public void ReadUInt64_CorrectlyReadsBytes()
    {
        byte[] data = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };
        ulong value = BigEndian.ReadUInt64(data);
        Assert.Equal((double)0x1234567890ABCDEF, value);
    }
}