using Core.Networking;
using RealTimeUdpStream.Core.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WPFUI_NEW.Services
{
    public delegate void FrameReadyHandler(BitmapSource frameSource);

    public class ScreenReceiver : IDisposable
    {
        private readonly UdpPeer _peer;
        private bool _isReceiving = false;
        public event FrameReadyHandler OnFrameReady;

        // Backbuffer để chứa toàn bộ hình ảnh hiện tại
        // Dùng WriteableBitmap để có thể cập nhật từng phần
        private WriteableBitmap _backBuffer;
        private object _bufferLock = new object();

        public ScreenReceiver(UdpPeer peer)
        {
            _peer = peer;
            _peer.OnPacketReceived += Peer_OnPacketReceived;
        }

        public void Start() { _isReceiving = true; Task.Run(() => _peer.StartReceivingAsync()); }
        public void Stop() { _isReceiving = false; }

        private void Peer_OnPacketReceived(UdpPacket packet)
        {
            if (packet.Header.PacketType != (byte)UdpPacketType.Video) return;

            using (packet)
            {
                // Chuyển tác vụ giải nén sang ThreadPool để không chặn luồng mạng
                var payloadCopy = packet.Payload; // Struct copy
                var header = packet.Header;

                // Cảnh báo: packet.Payload buffer sẽ được trả về pool khi hết using.
                // Cần copy dữ liệu ra mảng riêng để xử lý trên luồng khác hoặc xử lý đồng bộ tại đây.
                // Để đơn giản và an toàn bộ nhớ, xử lý đồng bộ:

                ProcessVideoFrame(header, payloadCopy);
            }
        }

        private void ProcessVideoFrame(UdpPacketHeader header, ArraySegment<byte> data)
        {
            try
            {
                bool isKeyFrame = (header.Flags & (byte)PacketFlags.IsKeyframe) != 0;
                int x = header.RectX;
                int y = header.RectY;
                int w = header.RectW;
                int h = header.RectH;

                // 1. Giải nén JPEG thành BitmapImage
                BitmapImage partialBmp = DecodeJpeg(data);
                if (partialBmp == null) return;

                // 2. Cập nhật UI (phải chạy trên UI Thread vì WriteableBitmap thuộc UI)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_bufferLock)
                    {
                        if (isKeyFrame)
                        {
                            // KeyFrame: Tạo mới Backbuffer
                            _backBuffer = new WriteableBitmap(partialBmp);
                        }
                        else
                        {
                            // Incremental: Vẽ đè lên Backbuffer cũ
                            if (_backBuffer == null) return; // Chưa có keyframe đầu tiên, bỏ qua

                            // Đảm bảo kích thước khớp hoặc xử lý resize nếu cần (đơn giản hóa ở đây)
                            int stride = (partialBmp.PixelWidth * partialBmp.Format.BitsPerPixel + 7) / 8;
                            int size = stride * partialBmp.PixelHeight;
                            byte[] pixelData = new byte[size];
                            partialBmp.CopyPixels(pixelData, stride, 0);

                            // Viết đè pixels vào vị trí (x, y)
                            Int32Rect destRect = new Int32Rect(x, y, partialBmp.PixelWidth, partialBmp.PixelHeight);

                            // Kiểm tra biên giới hạn
                            if (x + destRect.Width <= _backBuffer.PixelWidth && y + destRect.Height <= _backBuffer.PixelHeight)
                            {
                                _backBuffer.WritePixels(destRect, pixelData, stride, 0);
                            }
                        }

                        // Gửi Backbuffer (đã cập nhật) ra UI để hiển thị
                        // Clone và Freeze để an toàn giữa các luồng nếu View binding trực tiếp
                        // Nhưng vì WriteableBitmap đã ở UI thread, ta có thể gửi trực tiếp hoặc Clone.
                        // Để tối ưu, View nên bind trực tiếp vào 1 property giữ WriteableBitmap này.
                        // Tuy nhiên, giữ nguyên Interface OnFrameReady:
                        OnFrameReady?.Invoke(_backBuffer);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Frame processing error: {ex.Message}");
            }
        }

        private BitmapImage DecodeJpeg(ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count == 0) return null;
            try
            {
                using (var ms = new MemoryStream(data.Array, data.Offset, data.Count))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { return null; }
        }

        public void Dispose() { if (_peer != null) _peer.OnPacketReceived -= Peer_OnPacketReceived; }
    }
}