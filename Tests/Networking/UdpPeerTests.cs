using Core.Networking;
using RealtimeUdpStream.Core.Networking;
using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Networking;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class UdpPeerTests
{
    private readonly ITestOutputHelper _output;

    public UdpPeerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SendDataAsync_FragmentsAndReassemblesCorrectly()
    {
        var serverPort = 12000;
        var clientPort = 12001;
        var server = new UdpPeer(serverPort);

        // Khởi tạo client ở một cổng khác để lắng nghe
        var client = new UdpPeer(clientPort);
        var serverEndPoint = new IPEndPoint(IPAddress.Loopback, serverPort);
        var clientEndPoint = new IPEndPoint(IPAddress.Loopback, clientPort);

        var receivedPackets = new ConcurrentBag<UdpPacket>();

        client.OnPacketReceived += receivedPackets.Add;

        var clientTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var clientTask = Task.Run(async () => {
            try { await client.StartReceivingAsync(); } catch { }
        }, clientTokenSource.Token);

        var largeData = new byte[2000];
        new Random().NextBytes(largeData);

        // Vòng lặp gửi dữ liệu
        int maxPayloadSize = server.MaximumTransferUnit - PacketBuilder.HeaderSize;
        int totalChunks = (int)Math.Ceiling((double)largeData.Length / maxPayloadSize);
        var sequenceNumber = 123; // Sử dụng một số cố định cho test

        for (ushort i = 0; i < totalChunks; i++)
        {
            int chunkOffset = i * maxPayloadSize;
            int chunkSize = Math.Min(maxPayloadSize, largeData.Length - chunkOffset);

            var chunkPayload = new byte[maxPayloadSize];
            Array.Copy(largeData, chunkOffset, chunkPayload, 0, chunkSize);

            var header = new UdpPacketHeader
            {
                Version = 1,
                PacketType = (byte)UdpPacketType.Video,
                Flags = (byte)PacketFlags.None,
                SequenceNumber = (uint)sequenceNumber,
                TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond),
                TotalChunks = (ushort)totalChunks,
                ChunkId = i,
            };

            var packet = new UdpPacket(header, chunkPayload);
            await server.SendToAsync(packet, clientEndPoint); // Gửi đến client
        }

        await Task.Delay(100);

        client.Dispose();
        server.Dispose();
        clientTokenSource.Cancel();

        await clientTask;

        var reassembledPacket = receivedPackets.FirstOrDefault();
        Assert.NotNull(reassembledPacket);
        Assert.Equal(largeData, reassembledPacket.Payload.Take(largeData.Length).ToArray());
    }
}