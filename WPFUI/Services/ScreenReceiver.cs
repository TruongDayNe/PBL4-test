using Core.Networking;
using RealTimeUdpStream.Core.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace WPFUI.Services
{
    public delegate void FrameReadyHandler(BitmapSource frameSource);

    public class ScreenReceiver : IDisposable
    {
        private readonly UdpPeer _peer;
        private bool _isReceiving = false;

        public event FrameReadyHandler OnFrameReady;

        public ScreenReceiver(int listenPort)
        {
            _peer = new UdpPeer(listenPort);
            // Đăng ký vào sự kiện của UdpPeer, nơi sẽ trả về các frame đã hoàn chỉnh
            _peer.OnPacketReceived += Peer_OnPacketReceived;
        }

        public void Start()
        {
            if (_isReceiving) return;
            _isReceiving = true;
            Task.Run(() => _peer.StartReceivingAsync());
        }

        public void Stop()
        {
            if (!_isReceiving) return;
            _isReceiving = false;
            //_peer.Stop(); // UdpPeer đã có sẵn phương thức Stop
        }

        /// <summary>
        /// Được gọi bởi UdpPeer mỗi khi có một frame HOÀN CHỈNH (đã được ghép và/hoặc phục hồi).
        /// </summary>
        private void Peer_OnPacketReceived(UdpPacket packet)
        {
            // Chỉ xử lý các gói tin Video
            if (packet.Header.PacketType != (byte)UdpPacketType.Video)
            {
                // Nếu packet này mượn buffer từ pool, hãy chắc chắn trả lại nó
                packet.Dispose();
                return;
            }

            // Gói packet sẽ tự động trả buffer về pool khi ra khỏi khối using
            using (packet)
            {
                var bitmapSource = ConvertBytesToBitmapSource(packet.Payload);
                if (bitmapSource != null)
                {
                    OnFrameReady?.Invoke(bitmapSource);
                }
            }
        }

        private BitmapSource ConvertBytesToBitmapSource(ArraySegment<byte> imageBytes)
        {
            if (imageBytes.Array == null || imageBytes.Count == 0) return null;
            try
            {
                // MemoryStream có thể làm việc trực tiếp với ArraySegment
                using (var ms = new MemoryStream(imageBytes.Array, imageBytes.Offset, imageBytes.Count))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Quan trọng để tối ưu cho WPF
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting bytes to image: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            Stop();
            _peer?.Dispose();
        }
    }
}