using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RealtimeUdpStream.Core.Networking
{
    public static class FecXor
    {
        public static UdpPacket CreateParityPacket(IEnumerable<UdpPacket> dataPackets)
        {
            var dataPacketList = dataPackets.ToList();
            if (!dataPacketList.Any()) return null;

            int payloadLength = dataPacketList.First().Payload.Count;
            var parityPayloadBuffer = BytePool.Rent(payloadLength);

            try
            {
                var parityPayloadSpan = parityPayloadBuffer.AsSpan(0, payloadLength);
                parityPayloadSpan.Clear();

                foreach (var packet in dataPacketList)
                {
                    var sourcePayload = packet.Payload;
                    for (int i = 0; i < payloadLength; i++)
                    {
                        // Dùng .Array và .Offset để truy cập
                        parityPayloadSpan[i] ^= sourcePayload.Array[sourcePayload.Offset + i];
                    }
                }

                var fecHeader = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Fec, Version = 1 };
                var payloadSegment = new ArraySegment<byte>(parityPayloadBuffer, 0, payloadLength);

                return new UdpPacket(fecHeader, payloadSegment) { IsPayloadFromPool = true };
            }
            catch
            {
                BytePool.Return(parityPayloadBuffer);
                throw;
            }
        }

        public static UdpPacket RecoverPacket(UdpPacket fecPacket, IEnumerable<UdpPacket> receivedPackets)
        {
            if (fecPacket == null || !receivedPackets.Any()) return null;

            int payloadLength = fecPacket.Payload.Count;
            var recoveredPayloadBuffer = BytePool.Rent(payloadLength);

            try
            {
                var recoveredPayloadSpan = recoveredPayloadBuffer.AsSpan(0, payloadLength);
                // Dùng Buffer.BlockCopy cho hiệu năng cao hơn khi copy
                Buffer.BlockCopy(fecPacket.Payload.Array, fecPacket.Payload.Offset, recoveredPayloadBuffer, 0, payloadLength);

                foreach (var packet in receivedPackets)
                {
                    var sourcePayload = packet.Payload;
                    for (int i = 0; i < payloadLength; i++)
                    {
                        // SỬA LỖI Ở ĐÂY: Dùng .Array và .Offset để truy cập
                        recoveredPayloadSpan[i] ^= sourcePayload.Array[sourcePayload.Offset + i];
                    }
                }

                var recoveredHeader = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Video };
                var payloadSegment = new ArraySegment<byte>(recoveredPayloadBuffer, 0, payloadLength);

                return new UdpPacket(recoveredHeader, payloadSegment) { IsPayloadFromPool = true };
            }
            catch
            {
                BytePool.Return(recoveredPayloadBuffer);
                throw;
            }
        }
    }
}