using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace RealtimeUdpStream.Core.Networking
{
    public static class FecXor
    {
        /// <summary>
        /// Tạo gói tin phục hồi (parity shard) bằng cách XOR các gói tin dữ liệu.
        /// Giả định các gói tin dữ liệu có cùng kích thước payload.
        /// </summary>
        public static UdpPacket CreateParityPacket(IEnumerable<UdpPacket> dataPackets)
        {
            var dataPacketList = dataPackets.ToList();
            if (!dataPacketList.Any())
            {
                return null;
            }

            int payloadLength = dataPacketList.First().Payload.Length;
            var parityPayload = BytePool.Rent(payloadLength);

            try
            {
                // Khởi tạo mảng về 0 để tránh lỗi từ dữ liệu cũ trong pool
                Array.Clear(parityPayload, 0, payloadLength);

                foreach (var packet in dataPacketList)
                {
                    for (int i = 0; i < payloadLength; i++)
                    {
                        parityPayload[i] ^= packet.Payload[i];
                    }
                }

                var fecHeader = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Fec, Version = 1 };
                var finalParityPayload = new byte[payloadLength];
                Array.Copy(parityPayload, finalParityPayload, payloadLength);
                return new UdpPacket(fecHeader, finalParityPayload);
            }
            finally
            {
                // Đảm bảo mảng được trả lại vào pool
                BytePool.Return(parityPayload);
            }
        }

        /// <summary>
        /// Phục hồi một gói tin bị mất bằng cách XOR các gói tin còn lại với gói tin parity.
        /// </summary>
        public static UdpPacket RecoverPacket(UdpPacket fecPacket, IEnumerable<UdpPacket> receivedPackets)
        {
            if (fecPacket == null || !receivedPackets.Any())
            {
                return null;
            }

            var recoveredPayload = new byte[fecPacket.Payload.Length];
            Array.Copy(fecPacket.Payload, recoveredPayload, recoveredPayload.Length);

            foreach (var packet in receivedPackets)
            {
                for (int i = 0; i < recoveredPayload.Length; i++)
                {
                    recoveredPayload[i] ^= packet.Payload[i];
                }
            }

            var recoveredHeader = new UdpPacketHeader { PacketType = (byte)UdpPacketType.Video };

            return new UdpPacket(recoveredHeader, recoveredPayload);
        }
    }
}