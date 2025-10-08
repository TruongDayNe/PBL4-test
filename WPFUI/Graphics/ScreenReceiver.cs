using Core.Networking;
using RealTimeUdpStream.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace WPFUI.Graphics
{
    public delegate void FrameReadyHandler(BitmapSource frameSource);

    public class ScreenReceiver : IDisposable
    {
        private readonly UdpPeer _peer;
        private readonly ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>> _frameBuffers = new ConcurrentDictionary<uint, ConcurrentDictionary<ushort, UdpPacket>>();
        private bool _isReceiving = false;

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

            var chunkBuffer = _frameBuffers.GetOrAdd(sequence, _ => new ConcurrentDictionary<ushort, UdpPacket>());
            chunkBuffer.TryAdd(header.ChunkId, packet);

            if (chunkBuffer.Count == header.TotalChunks)
            {
                Task.Run(() => ReassembleAndDisplay(sequence));
            }

            CleanUpOldFrames(sequence);
        }

        private void ReassembleAndDisplay(uint sequence)
        {
            if (!_frameBuffers.TryRemove(sequence, out var chunkBuffer)) return;

            var sortedChunks = chunkBuffer.OrderBy(c => c.Key).Select(c => c.Value.Payload);
            using (var ms = new MemoryStream())
            {
                foreach (var payload in sortedChunks)
                {
                    ms.Write(payload, 0, payload.Length);
                }
                var bitmapSource = ConvertBytesToBitmapSource(ms.ToArray());
                if (bitmapSource != null)
                {
                    OnFrameReady?.Invoke(bitmapSource);
                }
            }
        }

        private BitmapSource ConvertBytesToBitmapSource(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0) return null;
            try
            {
                using (var ms = new MemoryStream(imageBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting bytes to image: {ex.Message}");
                return null;
            }
        }

        private void CleanUpOldFrames(uint currentSequence)
        {
            var framesToClear = _frameBuffers.Keys.Where(seq => seq < currentSequence && currentSequence - seq > 10).ToList();
            foreach (var seq in framesToClear)
            {
                _frameBuffers.TryRemove(seq, out _);
            }
        }

        public void Dispose()
        {
            Stop();
            _peer?.Dispose();
        }
    }
}