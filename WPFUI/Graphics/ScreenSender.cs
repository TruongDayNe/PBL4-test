using Core.Networking;
using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Networking;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO; 
using System.Net; 
using System.Threading;
using System.Threading.Tasks;

namespace WPFUI.Graphics 
{
    public class ScreenSender : IDisposable
    {
        // 30 FPS -> 1000ms / 30 = 33.3ms
        public const int FRAMERATE_MS = 33;

        private ScreenProcessor _screenProcessor = ScreenProcessor.Instance;
        private UdpPeer _server;
        private IPEndPoint _clientEndPoint;
        private static readonly ImageCodecInfo JpegCodec = GetEncoder(ImageFormat.Jpeg);

        // EncoderParams để cấu hình chất lượng nén JPEG (Ví dụ: 80/100)
        private EncoderParameters encoderParams;

        public ScreenSender(string clientIp, int clientPort, int serverPort, ScreenProcessor processor)
        {
            _server = new UdpPeer(serverPort);
            _clientEndPoint = new IPEndPoint(IPAddress.Parse(clientIp), clientPort);
            _screenProcessor = processor; // Gán ScreenProcessor được truyền vào

            encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }


        // Loại bỏ tham số cũ, sử dụng _server và _clientEndPoint
        public async Task SendScreenLoopAsync(CancellationToken token)
        {
            uint sequenceNumber = 0; // Tăng dần theo mỗi khung hình

            while (!token.IsCancellationRequested) // Vòng lặp stream vô tận
            {
                try
                {
                    byte[] largeData;
                    try
                    {

                        // 1. CHỤP và NÉN 
                        using (System.Drawing.Image img = _screenProcessor.CurrentScreenImage)
                        using (var ms = new MemoryStream())
                        {
                            if (img == null) continue; // Bỏ qua nếu không chụp được ảnh

                            // Nén ảnh thành JPEG byte array
                            img?.Save(ms, JpegCodec, encoderParams);
                            largeData = ms.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Screen Capture Error: {ex.Message}");
                        continue; // Bỏ qua khung hình này nếu lỗi
                    }

                    // 2. CHIA GÓI và GỬI
                    int maxPayloadSize = _server.MaximumTransferUnit - PacketBuilder.HeaderSize;
                    int totalChunks = (int)Math.Ceiling((double)largeData.Length / maxPayloadSize);

                    sequenceNumber++; // Tăng ID khung hình

                    for (ushort i = 0; i < totalChunks; i++)
                    {
                        int chunkOffset = i * maxPayloadSize;
                        int chunkSize = Math.Min(maxPayloadSize, largeData.Length - chunkOffset);

                        // Tạo mảng payload nhỏ
                        var chunkPayload = new byte[chunkSize];
                        Array.Copy(largeData, chunkOffset, chunkPayload, 0, chunkSize);

                        // Khởi tạo Header tùy chỉnh
                        var header = new UdpPacketHeader
                        {
                            Version = 1,
                            PacketType = (byte)UdpPacketType.Video,
                            Flags = (byte)PacketFlags.None,
                            SequenceNumber = sequenceNumber,
                            TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond),
                            TotalChunks = (ushort)totalChunks,
                            ChunkId = i,
                        };

                        var packet = new UdpPacket(header, chunkPayload);
                        // Gửi gói tin
                        await _server.SendToAsync(packet, _clientEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Streaming Error: {ex.Message}");
                    // Có thể thêm logic dừng hoặc reconnect ở đây
                }

                // 3. KIỂM SOÁT TỐC ĐỘ (giới hạn 30 FPS)
                await Task.Delay(FRAMERATE_MS, token);
            }
        }

        public void Dispose()
        {
            _server?.Dispose();
        }

        #region TESTING
        // Đặt phương thức này bên trong lớp public class ScreenSender
        // Sửa đổi chữ ký phương thức để trả về một Image
        public async Task<System.Drawing.Image> SendSingleFrameAsync()
        {
            uint testSequenceNumber = (uint)(DateTime.UtcNow.Ticks % uint.MaxValue);
            Debug.WriteLine($"--- Bắt đầu gửi Frame Test #{testSequenceNumber} ---");

            try
            {
                byte[] largeData;
                System.Drawing.Image imageForPreview = null; // Biến để lưu ảnh và trả về

                // 1. CHỤP và NÉN ẢNH
                try
                {
                    using (System.Drawing.Image img = _screenProcessor.CurrentScreenImage)
                    {
                        if (img == null)
                        {
                            Debug.WriteLine("Test Error: Không thể chụp ảnh màn hình (img is null).");
                            return null; // Trả về null nếu không chụp được ảnh
                        }

                        // QUAN TRỌNG: Tạo một bản sao (clone) của ảnh để trả về.
                        // Lý do: Đối tượng 'img' gốc sẽ bị hủy (dispose) ngay khi ra khỏi khối 'using'.
                        imageForPreview = (System.Drawing.Image)img.Clone();

                        using (var ms = new MemoryStream())
                        {
                            // Nén ảnh gốc (img), không phải bản sao
                            img.Save(ms, JpegCodec, encoderParams);
                            largeData = ms.ToArray();
                            Debug.WriteLine($"Frame Test #{testSequenceNumber} đã được nén thành {largeData.Length} bytes.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Test Error - Lỗi khi chụp hoặc nén ảnh: {ex.Message}");
                    return null; // Trả về null nếu có lỗi
                }

                // 2. CHIA GÓI và GỬI
                int maxPayloadSize = _server.MaximumTransferUnit - PacketBuilder.HeaderSize;
                int totalChunks = (int)Math.Ceiling((double)largeData.Length / maxPayloadSize);
                Debug.WriteLine($"Frame Test #{testSequenceNumber} sẽ được chia thành {totalChunks} gói tin.");

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
                        Flags = (byte)PacketFlags.None,
                        SequenceNumber = testSequenceNumber,
                        TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond),
                        TotalChunks = (ushort)totalChunks,
                        ChunkId = i,
                    };

                    var packet = new UdpPacket(header, chunkPayload);
                    await _server.SendToAsync(packet, _clientEndPoint);
                }

                Debug.WriteLine($"--- Đã gửi xong Frame Test #{testSequenceNumber} ---");

                // Trả về bản sao của ảnh đã chụp
                return imageForPreview;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Test Error - Lỗi nghiêm trọng khi gửi frame: {ex.Message}");
                return null; // Trả về null nếu có lỗi
            }
        }
        #endregion
    }
}