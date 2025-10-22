using Core.Networking; // Thêm using cho UdpPeer
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

namespace WPFUI_NEW.Services
{
    public delegate void FrameCapturedHandler(Image frame);

    public class ScreenSender : IDisposable
    {
        public event FrameCapturedHandler OnFrameCaptured;

        public const int FRAMERATE_MS = 33; // ~30 FPS
        private const int FEC_GROUP_SIZE = 10; // Cứ 10 gói data thì tạo 1 gói FEC

        private readonly UdpPeer _server;
        private readonly IPEndPoint _clientEndPoint;
        private readonly ScreenProcessor _screenProcessor;
        private static readonly ImageCodecInfo JpegCodec = GetEncoder(ImageFormat.Jpeg);
        private readonly EncoderParameters _encoderParams;

        public ScreenSender(string clientIp, int clientPort, int serverPort, ScreenProcessor processor)
        {
            _server = new UdpPeer(serverPort);
            _clientEndPoint = new IPEndPoint(IPAddress.Parse(clientIp), clientPort);
            _screenProcessor = processor;

            _encoderParams = new EncoderParameters(1);
            _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L); // Giảm chất lượng để tối ưu băng thông
        }

        public async Task SendScreenLoopAsync(CancellationToken token)
        {
            uint sequenceNumber = 0;

            while (!token.IsCancellationRequested)
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    byte[] largeData;
                    Image imageForPreview = null;

                    // 1. Chụp ảnh
                    using (Image img = _screenProcessor.CurrentScreenImage)
                    {
                        if (img == null) continue;

                        // 2. Thay đổi kích thước (Tùy chọn, bỏ comment nếu muốn dùng)
                        // using (var resizedImg = new Bitmap(img, new Size(1280, 720)))
                        // {
                        //     imageForPreview = (Image)resizedImg.Clone();
                        //     using (var ms = new MemoryStream())
                        //     {
                        //         resizedImg.Save(ms, JpegCodec, _encoderParams);
                        //         largeData = ms.ToArray();
                        //     }
                        // }

                        // Nén ảnh gốc
                        imageForPreview = (Image)img.Clone();
                        using (var ms = new MemoryStream())
                        {
                            img.Save(ms, JpegCodec, _encoderParams);
                            largeData = ms.ToArray();
                        }
                    }

                    // 3. Kích hoạt event để UI hiển thị preview
                    OnFrameCaptured?.Invoke(imageForPreview);

                    // 4. Chia gói, gom nhóm FEC và gửi đi
                    int maxPayloadSize = _server.MaximumTransferUnit - PacketBuilder.HeaderSize;
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

                        await _server.SendToAsync(dataPacket, _clientEndPoint);
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
                                    await _server.SendToAsync(fecPacket, _clientEndPoint);
                                }
                            }

                            foreach (var p in dataPacketsInGroup) p.Dispose();
                            dataPacketsInGroup.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Streaming Error: {ex.Message}");
                }

                // 5. Điều chỉnh tốc độ gửi
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
            _server?.Dispose();
        }
    }
}