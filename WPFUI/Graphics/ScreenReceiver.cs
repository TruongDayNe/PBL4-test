using Core.Networking;
using RealTimeUdpStream.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Add reference to PresentationCore.dll and WindowsBase.dll in your project if not already present.
// Change the using directive to the correct namespace for BitmapSource and BitmapImage:
using System.Windows.Media.Imaging;

namespace WPGUI.Graphics
{
    // Delegate để thông báo khi một khung hình hoàn chỉnh sẵn sàng hiển thị
    public delegate void FrameReadyHandler(BitmapSource frameSource);

    public class ScreenReceiver : IDisposable
    {
        private readonly UdpPeer _peer;
        // Replace this line:
        // private readonly ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>> _frameBuffers = new();

        // With this line:
        private readonly ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>> _frameBuffers = new ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>>();
        private bool _isReceiving = false;

        // Sự kiện sẽ được bắn ra khi có khung hình hoàn chỉnh
        public event FrameReadyHandler OnFrameReady;

        public ScreenReceiver(int listenPort)
        {
            _peer = new UdpPeer(listenPort);
            _peer.OnPacketReceived += HandlePacketReceived;
        }

        public void Start()
        {
            if (_isReceiving) return;
            _isReceiving = true;
            // Bắt đầu lắng nghe trên luồng nền
            Task.Run(async () =>
            {
                try { await _peer.StartReceivingAsync(); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Receiver loop stopped: {ex.Message}");
                }
            });
        }

        public void Stop()
        {
            _isReceiving = false;
            _peer.Stop();
        }

        private void HandlePacketReceived(UdpPacket packet)
        {
            var header = packet.Header;
            var sequence = header.SequenceNumber;
            var chunkId = header.ChunkId;

            // 1. Thêm gói tin vào buffer của khung hình tương ứng
            // Lấy hoặc tạo buffer cho khung hình này
            var chunkBuffer = _frameBuffers.GetOrAdd(sequence, _ => new ConcurrentDictionary<ushort, UdpPacket>());

            // Chỉ thêm vào nếu nó chưa được nhận (UDP có thể bị trùng lặp)
            chunkBuffer.TryAdd(chunkId, packet);

            // 2. Kiểm tra xem khung hình đã hoàn chỉnh chưa
            if (chunkBuffer.Count == header.TotalChunks)
            {
                // Tái hợp khung hình trên luồng nền để không chặn luồng nhận UDP
                Task.Run(() => ReassembleAndDisplay(sequence, header.TotalChunks));
            }

            // 3. (Tùy chọn) Xóa các frame cũ để tránh tràn bộ nhớ nếu có lỗi mất gói tin
            CleanUpOldFrames(sequence);
        }

        private void ReassembleAndDisplay(uint sequence, ushort totalChunks)
        {
            if (!_frameBuffers.TryRemove(sequence, out var chunkBuffer))
            {
                return; // Không tìm thấy hoặc đã được xử lý
            }

            // Kiểm tra lần cuối: Đảm bảo có đủ số lượng gói tin
            if (chunkBuffer.Count != totalChunks)
            {
                // Nếu thiếu (do mất gói tin), bỏ qua khung hình này
                return;
            }

            // 1. Tái hợp mảng byte hình ảnh
            var assembledBytes = new List<byte>();

            // Sắp xếp các gói tin theo ChunkId
            var sortedChunks = chunkBuffer.OrderBy(c => c.Key).Select(c => c.Value.Payload);

            foreach (var payload in sortedChunks)
            {
                assembledBytes.AddRange(payload);
            }

            // 2. Chuyển đổi byte array (JPEG) sang BitmapSource
            var imageBytes = assembledBytes.ToArray();
            var bitmapSource = ConvertBytesToBitmapSource(imageBytes);

            if (bitmapSource != null)
            {
                // 3. Bắn sự kiện FrameReady để UI có thể hiển thị
                OnFrameReady?.Invoke(bitmapSource);
            }
        }

        // Phương thức quan trọng: Chuyển mảng byte JPEG sang BitmapSource
        private BitmapSource ConvertBytesToBitmapSource(byte[] imageBytes)
        {
            try
            {
                using (var ms = new MemoryStream(imageBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Tối ưu hóa cho WPF
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                // Gói tin bị hỏng (corrupt) hoặc không phải là JPEG hợp lệ
                Debug.WriteLine($"Error converting bytes to image: {ex.Message}");
                return null;
            }
        }

        // Dọn dẹp các frame cũ hơn để tránh buffer overflow
        private void CleanUpOldFrames(uint currentSequence)
        {
            // Xóa tất cả các khung hình có ID nhỏ hơn (cũ hơn)
            var framesToClear = _frameBuffers.Keys.Where(seq => seq < currentSequence).ToList();
            foreach (var seq in framesToClear)
            {
                _frameBuffers.TryRemove(seq, out _);
            }
        }

        public void Dispose()
        {
            Stop();
            _peer.Dispose();
        }
    }
}