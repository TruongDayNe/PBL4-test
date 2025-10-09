using RealtimeUdpStream.Core.Networking;
using RealTimeUdpStream.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class FecXorTests
{
    [Fact]
    public void CreateParityPacket_CorrectlyXorsData_WithFixedLengthPayload()
    {
        var header1 = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Video, SequenceNumber = 1 };
        var payload1 = new ArraySegment<byte>(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var packet1 = new UdpPacket(header1, payload1);

        var header2 = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Video, SequenceNumber = 2 };
        var payload2 = new ArraySegment<byte>(new byte[] { 0x05, 0x06, 0x07, 0x08 });
        var packet2 = new UdpPacket(header2, payload2);

        var packets = new List<UdpPacket> { packet1, packet2 };
        var parityPacket = FecXor.CreateParityPacket(packets);

        Assert.NotNull(parityPacket);
        Assert.Equal(new byte[] { 0x04, 0x04, 0x04, 0x0C }, parityPacket.Payload.ToArray());
    }

    [Fact]
    public void RecoverPacket_CorrectlyRecoversLostPacket_WithFixedLengthPayload()
    {
        var header1 = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Video, SequenceNumber = 1 };
        var payload1 = new ArraySegment<byte>(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var packet1 = new UdpPacket(header1, payload1);

        var header2 = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Video, SequenceNumber = 2 };
        var payload2 = new ArraySegment<byte>(new byte[] { 0x05, 0x06, 0x07, 0x08 });
        var packet2 = new UdpPacket(header2, payload2);

        var header3 = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Video, SequenceNumber = 3 };
        var payload3 = new ArraySegment<byte>(new byte[] { 0x09, 0x0a, 0x0b, 0x0c });
        var packet3 = new UdpPacket(header3, payload3);

        var allPackets = new List<UdpPacket> { packet1, packet2, packet3 };
        var parityPacket = FecXor.CreateParityPacket(allPackets);

        var receivedPackets = new List<UdpPacket> { packet1, packet3 };

        var recoveredPacket = FecXor.RecoverPacket(parityPacket, receivedPackets);

        Assert.NotNull(recoveredPacket);
        Assert.Equal(packet2.Payload, recoveredPacket.Payload);
    }
}
