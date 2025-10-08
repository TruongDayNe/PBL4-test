using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Networking;
using System;
using Xunit;

public class PacketTests
{
    private readonly PacketBuilder _builder = new PacketBuilder();
    private readonly PacketParser _parser = new PacketParser();

    [Fact]
    public void Packet_BuilderAndParser_WorkTogetherCorrectly_WithChecksum()
    {
        // 1. Chuẩn bị dữ liệu
        var header = new UdpPacketHeader
        {
            Version = 1,
            PacketType = (byte)UdpPacketType.Video,
            Flags = (byte)PacketFlags.IsKeyframe,
            SequenceNumber = 0x12345678,
            TimestampMs = 0x1122334455667788,
            Checksum = 0, // Checksum sẽ được tính sau
            TotalChunks = 1,
            ChunkId = 0
        };

        // 2. Tạo một gói tin mẫu với payload
        int payloadSize = 50;
        Span<byte> packetBuffer = new byte[PacketBuilder.HeaderSize + payloadSize];
        _builder.WriteHeader(header, packetBuffer);

        // Tạo payload giả
        for (int i = 0; i < payloadSize; i++)
        {
            packetBuffer[PacketBuilder.HeaderSize + i] = (byte)i;
        }

        // 3. Sử dụng builder để tính và ghi checksum
        _builder.WriteChecksum(packetBuffer);

        // 4. Sử dụng parser để đọc và kiểm tra
        var readResult = _parser.TryReadHeader(packetBuffer, out var parsedHeader);
        Assert.True(readResult);

        // Đảm bảo các giá trị header khớp
        Assert.Equal(header.Version, parsedHeader.Version);
        Assert.Equal(header.PacketType, parsedHeader.PacketType);
        Assert.Equal(header.SequenceNumber, parsedHeader.SequenceNumber);

        // 5. Kiểm tra checksum
        Assert.True(_parser.IsChecksumValid(packetBuffer));

        // 6. Mô phỏng lỗi (thay đổi 1 byte) và kiểm tra lại
        packetBuffer[PacketBuilder.HeaderSize + 10] = 0x01; // Thay đổi một byte bất kỳ
        Assert.False(_parser.IsChecksumValid(packetBuffer));
    }
}