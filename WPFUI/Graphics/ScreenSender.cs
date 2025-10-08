using Core.Networking;
using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Networking;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace WPFUI.Graphics
{
    // Delegate định nghĩa chữ ký cho event handler
    public delegate void FrameCapturedHandler(System.Drawing.Image frame);

    public class ScreenSender : IDisposable
    {
        // Sự kiện sẽ được kích hoạt mỗi khi một frame được chụp
        public event FrameCapturedHandler OnFrameCaptured;

        public const int FRAMERATE_MS = 33; // ~30 FPS

        private readonly ScreenProcessor _screenProcessor;
        private readonly UdpPeer _server;
        private readonly IPEndPoint _clientEndPoint;
        private static readonly ImageCodecInfo JpegCodec = GetEncoder(ImageFormat.Jpeg);
        private readonly EncoderParameters _encoderParams;

        public ScreenSender(string clientIp, int clientPort, int serverPort, ScreenProcessor processor)
        {
            _server = new UdpPeer(serverPort);
            _clientEndPoint = new IPEndPoint(IPAddress.Parse(clientIp), clientPort);
            _screenProcessor = processor;

            _encoderParams = new EncoderParameters(1);
            _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L); // Chất lượng JPEG
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

                    // 1. CHỤP ẢNH
                    using (Image img = _screenProcessor.CurrentScreenImage)
                    {
                        if (img == null) continue;

                        // Tạo bản sao để gửi qua event cho UI hiển thị
                        imageForPreview = (Image)img.Clone();

                        // Nén ảnh gốc để gửi đi
                        using (var ms = new MemoryStream())
                        {
                            img.Save(ms, JpegCodec, _encoderParams);
                            largeData = ms.ToArray();
                        }
                    }

                    // 2. KÍCH HOẠT EVENT ĐỂ HIỂN THỊ PREVIEW
                    OnFrameCaptured?.Invoke(imageForPreview);

                    // 3. CHIA GÓI VÀ GỬI
                    int maxPayloadSize = _server.MaximumTransferUnit - PacketBuilder.HeaderSize;
                    int totalChunks = (int)Math.Ceiling((double)largeData.Length / maxPayloadSize);
                    sequenceNumber++;

                    for (ushort i = 0; i < totalChunks; i++)
                    {
                        int chunkOffset = i * maxPayloadSize;
                        int chunkSize = Math.Min(maxPayloadSize, largeData.Length - chunkOffset);
                        var chunkPayload = new byte[chunkSize];
                        Array.Copy(largeData, chunkOffset, chunkPayload, 0, chunkSize);

                        var header = new UdpPacketHeader
                        {
                            Version = 1,
                            PacketType = (byte)UdpPacketType.Video,
                            SequenceNumber = sequenceNumber,
                            TotalChunks = (ushort)totalChunks,
                            ChunkId = i,
                        };
                        var packet = new UdpPacket(header, chunkPayload);
                        await _server.SendToAsync(packet, _clientEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Streaming Error: {ex.Message}");
                }

                // 4. ĐIỀU CHỈNH TỐC ĐỘ GỬI
                watch.Stop();
                var delay = FRAMERATE_MS - (int)watch.ElapsedMilliseconds;
                if (delay > 0)
                {
                    await Task.Delay(delay, token);
                }
            }
        }

        // Phương thức test gửi 1 frame
        public async Task<Image> SendSingleFrameAsync()
        {
            uint testSequenceNumber = (uint)(DateTime.UtcNow.Ticks % uint.MaxValue);
            try
            {
                byte[] largeData;
                Image imageForPreview = null;
                using (Image img = _screenProcessor.CurrentScreenImage)
                {
                    if (img == null) return null;
                    imageForPreview = (Image)img.Clone();
                    using (var ms = new MemoryStream())
                    {
                        img.Save(ms, JpegCodec, _encoderParams);
                        largeData = ms.ToArray();
                    }
                }
                int maxPayloadSize = _server.MaximumTransferUnit - PacketBuilder.HeaderSize;
                int totalChunks = (int)Math.Ceiling((double)largeData.Length / maxPayloadSize);
                for (ushort i = 0; i < totalChunks; i++)
                {
                    int chunkOffset = i * maxPayloadSize;
                    int chunkSize = Math.Min(maxPayloadSize, largeData.Length - chunkOffset);
                    var chunkPayload = new byte[chunkSize];
                    Array.Copy(largeData, chunkOffset, chunkPayload, 0, chunkSize);
                    var header = new UdpPacketHeader { SequenceNumber = testSequenceNumber, TotalChunks = (ushort)totalChunks, ChunkId = i };
                    var packet = new UdpPacket(header, chunkPayload);
                    await _server.SendToAsync(packet, _clientEndPoint);
                }
                return imageForPreview;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Test Error: {ex.Message}");
                return null;
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