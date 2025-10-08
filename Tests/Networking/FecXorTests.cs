using RealtimeUdpStream.Core.Networking;
using RealTimeUdpStream.Core.Models;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class FecXorTests
{
    [Fact]
    public void CreateParityPacket_CorrectlyXorsData_WithFixedLengthPayload()
    {
        var packet1 = new UdpPacket(UdpPacketType.Video, 1, new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var packet2 = new UdpPacket(UdpPacketType.Video, 2, new byte[] { 0x05, 0x06, 0x07, 0x08 });

        var packets = new List<UdpPacket> { packet1, packet2 };
        var parityPacket = FecXor.CreateParityPacket(packets);

        Assert.NotNull(parityPacket);
        Assert.Equal(new byte[] { 0x04, 0x04, 0x04, 0x0C }, parityPacket.Payload);
    }

    [Fact]
    public void RecoverPacket_CorrectlyRecoversLostPacket_WithFixedLengthPayload()
    {
        var packet1 = new UdpPacket(UdpPacketType.Video, 1, new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var packet2 = new UdpPacket(UdpPacketType.Video, 2, new byte[] { 0x05, 0x06, 0x07, 0x08 });
        var packet3 = new UdpPacket(UdpPacketType.Video, 3, new byte[] { 0x09, 0x0a, 0x0b, 0x0c });

        var allPackets = new List<UdpPacket> { packet1, packet2, packet3 };
        var parityPacket = FecXor.CreateParityPacket(allPackets);

        var receivedPackets = new List<UdpPacket> { packet1, packet3 };

        var recoveredPacket = FecXor.RecoverPacket(parityPacket, receivedPackets);

        Assert.NotNull(recoveredPacket);
        Assert.Equal(packet2.Payload, recoveredPacket.Payload);
    }
}