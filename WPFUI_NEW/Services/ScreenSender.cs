using Core.Networking;
using RealTimeUdpStream.Core.Networking;
using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;
using System.Collections.Concurrent;
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
        public const int FRAMERATE_MS = 33;
        private const int FEC_GROUP_SIZE = 10;
        private const int KEY_FRAME_INTERVAL = 30; // Gửi full frame mỗi 30 frame (~1 giây)

        private readonly UdpPeer _peer;
        private ConcurrentBag<IPEndPoint> _clients = new ConcurrentBag<IPEndPoint>();
        private readonly ScreenProcessor _screenProcessor;
        private static readonly ImageCodecInfo JpegCodec = GetEncoder(ImageFormat.Jpeg);
        private readonly EncoderParameters _encoderParams;

        private long _frameCount = 0;

        public int ClientCount => _clients.Count;

        public ScreenSender(UdpPeer peer, ScreenProcessor processor)
        {
            _peer = peer;
            _screenProcessor = processor;
            _encoderParams = new EncoderParameters(1);
            _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L); // Chất lượng 50
        }

        public void AddClient(IPEndPoint clientEndPoint) => _clients.Add(clientEndPoint);

        public void RemoveClient(IPEndPoint clientEndPoint)
        {
            var list = _clients.ToList();
            if (list.Remove(clientEndPoint)) _clients = new ConcurrentBag<IPEndPoint>(list);
        }

        public async Task SendScreenLoopAsync(CancellationToken token)
        {
            uint sequenceNumber = 0;

            while (!token.IsCancellationRequested)
            {
                var watch = Stopwatch.StartNew();

                if (_clients.IsEmpty)
                {
                    await Task.Delay(100, token);
                    continue;
                }

                try
                {
                    // Logic Drop Frame: Nếu mạng đang quá tải hoặc xử lý chậm, vòng lặp này sẽ tự delay
                    // do await SendToAllClientsAsync. Lần lặp tiếp theo sẽ lấy ảnh MỚI NHẤT từ Processor.

                    byte[] compressedData = null;
                    Rectangle sentRect = Rectangle.Empty;
                    bool isKeyFrame = (_frameCount % KEY_FRAME_INTERVAL == 0);

                    _screenProcessor.ProcessScreenImage((fullImg, dirtyRect) =>
                    {
                        if (fullImg == null) return;
                        Bitmap bmpToCompress = null;
                        bool needDisposeBmp = false;

                        try
                        {
                            // Quyết định gửi KeyFrame hay Partial
                            // Nếu dirtyRect rỗng và không phải KeyFrame -> Không có gì thay đổi -> Skip
                            if (!isKeyFrame && (dirtyRect.IsEmpty || dirtyRect.Width == 0 || dirtyRect.Height == 0))
                            {
                                return; // Skip processing
                            }

                            // Nếu là KeyFrame hoặc vùng thay đổi quá lớn (> 70% màn hình) -> Gửi Full
                            if (isKeyFrame || (dirtyRect.Width * dirtyRect.Height > fullImg.Width * fullImg.Height * 0.7))
                            {
                                isKeyFrame = true; // Force KeyFrame
                                sentRect = new Rectangle(0, 0, fullImg.Width, fullImg.Height);
                                bmpToCompress = (Bitmap)fullImg; // Dùng trực tiếp ảnh gốc
                            }
                            else
                            {
                                // Incremental Update: Cắt vùng DirtyRect
                                sentRect = dirtyRect;
                                bmpToCompress = new Bitmap(dirtyRect.Width, dirtyRect.Height);
                                needDisposeBmp = true;

                                using (var g = Graphics.FromImage(bmpToCompress))
                                {
                                    // Vẽ phần thay đổi vào bmp nhỏ
                                    g.DrawImage(fullImg,
                                        new Rectangle(0, 0, dirtyRect.Width, dirtyRect.Height),
                                        dirtyRect,
                                        GraphicsUnit.Pixel);
                                }
                            }

                            // Nén ảnh
                            using (var ms = new MemoryStream())
                            {
                                bmpToCompress.Save(ms, JpegCodec, _encoderParams);
                                compressedData = ms.ToArray();
                            }

                            // Preview UI (Chỉ gửi KeyFrame để UI đỡ lag, hoặc gửi fullImg nếu muốn mượt)
                            OnFrameCaptured?.Invoke(fullImg);
                        }
                        finally
                        {
                            if (needDisposeBmp && bmpToCompress != null)
                                bmpToCompress.Dispose();
                        }
                    });

                    if (compressedData == null)
                    {
                        // Không có dữ liệu để gửi (không thay đổi), đợi frame tiếp
                        await Task.Delay(10, token);
                        continue;
                    }

                    _frameCount++;
                    sequenceNumber++;

                    // Phân mảnh và gửi
                    await FragmentAndSendAsync(compressedData, sequenceNumber, isKeyFrame, sentRect);

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Streaming Error: {ex.Message}");
                }

                watch.Stop();
                // Priority: Low Latency -> Nếu xử lý lâu hơn quy định, chạy ngay frame tiếp theo
                var delay = FRAMERATE_MS - (int)watch.ElapsedMilliseconds;
                if (delay > 0) await Task.Delay(delay, token);
            }
        }

        private async Task FragmentAndSendAsync(byte[] data, uint sequence, bool isKeyFrame, Rectangle rect)
        {
            int maxPayloadSize = _peer.MaximumTransferUnit - PacketBuilder.HeaderSize;
            int totalChunks = (int)Math.Ceiling((double)data.Length / maxPayloadSize);
            var groupPackets = new List<UdpPacket>();

            for (ushort i = 0; i < totalChunks; i++)
            {
                int offset = i * maxPayloadSize;
                int size = Math.Min(maxPayloadSize, data.Length - offset);
                var chunkBuffer = BytePool.Rent(size);
                Buffer.BlockCopy(data, offset, chunkBuffer, 0, size);

                var header = new UdpPacketHeader
                {
                    Version = 1,
                    PacketType = (byte)UdpPacketType.Video,
                    SequenceNumber = sequence,
                    TimestampMs = (ulong)DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond,
                    TotalChunks = (ushort)totalChunks,
                    ChunkId = i,
                    Flags = isKeyFrame ? (byte)PacketFlags.IsKeyframe : (byte)0,
                    // Set coordinates info
                    RectX = (ushort)rect.X,
                    RectY = (ushort)rect.Y,
                    RectW = (ushort)rect.Width,
                    RectH = (ushort)rect.Height
                };

                var packet = new UdpPacket(header, new ArraySegment<byte>(chunkBuffer, 0, size))
                { IsPayloadFromPool = true };

                await SendToAllClientsAsync(packet);
                groupPackets.Add(packet);

                // FEC Logic
                if (groupPackets.Count == FEC_GROUP_SIZE || i == totalChunks - 1)
                {
                    using (var fecPacket = FecXor.CreateParityPacket(groupPackets))
                    {
                        if (fecPacket != null)
                        {
                            // Copy metadata quan trọng sang FEC
                            var fh = fecPacket.Header;
                            fh.SequenceNumber = sequence;
                            fh.TotalChunks = (ushort)totalChunks;
                            fh.ChunkId = groupPackets[0].Header.ChunkId;
                            fh.RectX = (ushort)rect.X; // Giữ metadata để recover nếu cần
                            fecPacket.Header = fh;
                            await SendToAllClientsAsync(fecPacket);
                        }
                    }
                    foreach (var p in groupPackets) p.Dispose();
                    groupPackets.Clear();
                }
            }
        }

        private async Task SendToAllClientsAsync(UdpPacket packet)
        {
            var tasks = new List<Task>();
            foreach (var client in _clients) tasks.Add(_peer.SendToAsync(packet, client));
            await Task.WhenAll(tasks);
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == format.Guid);
        }

        public void Dispose() { /* ... */ }
    }
}