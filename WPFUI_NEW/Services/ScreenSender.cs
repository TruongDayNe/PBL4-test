using Core.Networking;
using RealTimeUdpStream.Core.Networking;
using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RealtimeUdpStream.Core.Networking;
using System.Collections.Concurrent;

namespace WPFUI_NEW.Services
{
    public delegate void FrameCapturedHandler(Image frame);

    public class ScreenSender : IDisposable
    {
        public event FrameCapturedHandler OnFrameCaptured;

        public const int FRAMERATE_MS = 33; // ~30 FPS
        private const int FEC_GROUP_SIZE = 10;

        private readonly UdpPeer _peer;
        private readonly ConcurrentBag<IPEndPoint> _clients = new ConcurrentBag<IPEndPoint>(); // Danh sách client
        private readonly ScreenProcessor _screenProcessor;
        private static readonly ImageCodecInfo JpegCodec = GetEncoder(ImageFormat.Jpeg);
        private readonly EncoderParameters _encoderParams;

        public int ClientCount => _clients.Count;

        public ScreenSender(UdpPeer peer, ScreenProcessor processor)
        {
            _peer = peer; // peer được chia sẻ
            _screenProcessor = processor;

            _encoderParams = new EncoderParameters(1);
            _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L);
        }

        public void AddClient(IPEndPoint clientEndPoint)
        {
            _clients.Add(clientEndPoint);
            Debug.WriteLine($"[ScreenSender] Đã thêm client: {clientEndPoint}. Tổng số: {ClientCount}");
        }

        // THÊM HÀM MỚI NÀY:
        public void RemoveClient(IPEndPoint clientEndPoint)
        {
            // ConcurrentBag không có hàm Remove trực tiếp, chúng ta phải tạo lại list
            var currentClients = _clients.ToList();
            if (currentClients.Remove(clientEndPoint))
            {
                // Tạo lại ConcurrentBag từ danh sách đã cập nhật
                _clients = new ConcurrentBag<IPEndPoint>(currentClients);
                Debug.WriteLine($"[ScreenSender] Đã xóa client: {clientEndPoint}. Còn lại: {ClientCount}");
            }
        }

        private async Task SendToAllClientsAsync(UdpPacket packet)
        {
            if (_clients.IsEmpty) return;

            var sendTasks = new List<Task>(_clients.Count);
            foreach (var client in _clients)
            {
                sendTasks.Add(_peer.SendToAsync(packet, client));
            }
            await Task.WhenAll(sendTasks);
        }

        public async Task SendScreenLoopAsync(CancellationToken token)
        {
            uint sequenceNumber = 0;

            while (!token.IsCancellationRequested)
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    if (_clients.IsEmpty)
                    {
                        await Task.Delay(FRAMERATE_MS, token);
                        continue;
                    }

                    // --- ĐOẠN CODE THAY THẾ MỚI --- (BẮT ĐẦU)
                    byte[] largeData = null;

                    // SỬ DỤNG PHƯƠNG THỨC MỚI CỦA SCREEN PROCESSOR
                    _screenProcessor.ProcessScreenImage(img =>
                    {
                        // 'img' ở đây là ảnh GỐC, đang được ReadLock
                        if (img == null) return;

                        // 1. Nén ảnh gốc thành JPEG
                        using (var ms = new MemoryStream())
                        {
                            img.Save(ms, JpegCodec, _encoderParams);
                            largeData = ms.ToArray();
                        }

                        // 2. Gửi ảnh GỐC đi để preview
                        // KHÔNG CLONE() NỮA!
                        OnFrameCaptured?.Invoke(img);
                    });

                    if (largeData == null) // Bỏ qua nếu không lấy được ảnh
                    {
                        continue;
                    }

                    int maxPayloadSize = _peer.MaximumTransferUnit - PacketBuilder.HeaderSize;
                    int totalChunks = (int)Math.Ceiling((double)largeData.Length / maxPayloadSize);
                    sequenceNumber++;

                    var dataPacketsInGroup = new List<UdpPacket>();

                    for (ushort i = 0; i < totalChunks; i++)
                    {
                        int chunkOffset = i * maxPayloadSize;
                        int chunkSize = Math.Min(maxPayloadSize, largeData.Length - chunkOffset);

                        var chunkPayloadBuffer = BytePool.Rent(chunkSize);
                        Buffer.BlockCopy(largeData, chunkOffset, chunkPayloadBuffer, 0, chunkSize);
                        var payloadSegment = new ArraySegment<byte>(chunkPayloadBuffer, 0, chunkSize);

                        var header = new UdpPacketHeader
                        {
                            Version = 1,
                            PacketType = (byte)UdpPacketType.Video,
                            SequenceNumber = sequenceNumber,
                            TotalChunks = (ushort)totalChunks,
                            ChunkId = i
                        };

                        var dataPacket = new UdpPacket(header, payloadSegment) { IsPayloadFromPool = true };

                        await SendToAllClientsAsync(dataPacket);
                        dataPacketsInGroup.Add(dataPacket);

                        if (dataPacketsInGroup.Count == FEC_GROUP_SIZE || i == totalChunks - 1)
                        {
                            using (var fecPacket = FecXor.CreateParityPacket(dataPacketsInGroup))
                            {
                                if (fecPacket != null)
                                {
                                    var fecHeader = fecPacket.Header;
                                    fecHeader.SequenceNumber = sequenceNumber;
                                    fecHeader.TotalChunks = (ushort)totalChunks;
                                    fecHeader.ChunkId = dataPacketsInGroup.First().Header.ChunkId;
                                    fecPacket.Header = fecHeader;
                                    await SendToAllClientsAsync(fecPacket);
                                }
                            }

                            foreach (var p in dataPacketsInGroup) p.Dispose();
                            dataPacketsInGroup.Clear();
                        }
                        dataPacketsInGroup.Clear();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Streaming Error: {ex.Message}");
                }

                watch.Stop();
                var delay = FRAMERATE_MS - (int)watch.ElapsedMilliseconds;
                if (delay > 0)
                {
                    await Task.Delay(delay, token);
                }
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            return Array.Find(codecs, codec => codec.FormatID == format.Guid);
        }

        public void Dispose()
        {
            // ViewMode dispose resources if needed
            //_server?.Dispose();
        }
    }
}