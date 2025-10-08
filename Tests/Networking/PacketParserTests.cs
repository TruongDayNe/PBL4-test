using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Networking;
using System;
using Xunit;

public class PacketParserTests
{
    private readonly PacketBuilder _builder = new PacketBuilder();
    private readonly PacketParser _parser = new PacketParser();

    [Fact]
    public void IsChecksumValid_WithFletcher16_ReturnsTrueForValidData()
    {
        Span<byte> buffer = new byte[PacketParser.HeaderSize + 5];
        var header = new UdpPacketHeader
        {
            Version = 1,
            PacketType = (byte)UdpPacketType.Video,
            Flags = 0,
            SequenceNumber = 123,
            TimestampMs = 456,
            TotalChunks = 1,
            ChunkId = 0
        };
        _builder.WriteHeader(header, buffer);
        buffer.Slice(PacketParser.HeaderSize, 5).Fill(0xFF);

        // Ghi checksum hợp lệ
        _builder.WriteChecksum(buffer);

        Assert.True(_parser.IsChecksumValid(buffer));
    }
}