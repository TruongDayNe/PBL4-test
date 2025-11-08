using System;
using System.Collections.Concurrent;
using System.Linq;
using RealTimeUdpStream.Core.Models;
using System.Diagnostics;
using System.Threading;


namespace RealTimeUdpStream.Core.Networking
{
    public class NetworkStats
    {
        private readonly ConcurrentQueue<long> _pingHistory = new ConcurrentQueue<long>();
        private readonly ConcurrentQueue<DateTime> _packetSentTimestamps = new ConcurrentQueue<DateTime>();
        private readonly ConcurrentQueue<DateTime> _packetReceivedTimestamps = new ConcurrentQueue<DateTime>();
        private long _fecPacketsRecovered = 0;
        private long _lastFecPacketsRecovered = 0;

        //log để tính mất gói
        private readonly ConcurrentDictionary<uint, (long timestamp, bool acked)> _packetLog = new ConcurrentDictionary<uint, (long, bool)>();
        private readonly object _lock = new object();

        // --- BIẾN MỚI CHO BITRATE ---
        private long _bytesSentInSecond = 0;
        private long _bytesReceivedInSecond = 0;
        private long _lastSentBitrateKbps = 0;
        private long _lastReceivedBitrateKbps = 0;
        // Timer để biết khi nào 1 giây trôi qua
        private readonly Stopwatch _bitrateTimer = Stopwatch.StartNew();

        public void UpdateRtt(long sentTimestampMs)
        {
            long now = (long)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            long rttMs = now - sentTimestampMs;

            _pingHistory.Enqueue(rttMs);
            while (_pingHistory.Count > 10)
            {
                _pingHistory.TryDequeue(out _);
            }
        }
        public void LogFecPacketRecovered()
        {
            Interlocked.Increment(ref _fecPacketsRecovered);
        }

        public void LogPacketSent(uint sequenceNumber, int size)
        {
            _packetSentTimestamps.Enqueue(DateTime.UtcNow);
            _packetLog.TryAdd(sequenceNumber, (DateTime.UtcNow.Ticks, false));

            Interlocked.Add(ref _bytesSentInSecond, size);
        }

        public void LogPacketReceived(int size)
        {
            _packetReceivedTimestamps.Enqueue(DateTime.UtcNow);
            Interlocked.Add(ref _bytesReceivedInSecond, size);
        }

        public void LogPacketAcked(uint sequenceNumber)
        {
            if (_packetLog.TryGetValue(sequenceNumber, out var value))
            {
                _packetLog[sequenceNumber] = (value.timestamp, true);
            }
        }

        public TelemetrySnapshot GetSnapshot()
        {
            var oneSecondAgo = DateTime.UtcNow.AddSeconds(-1);
            while (_packetSentTimestamps.TryPeek(out var timestamp) && timestamp < oneSecondAgo)
            {
                _packetSentTimestamps.TryDequeue(out _);
            }
            while (_packetReceivedTimestamps.TryPeek(out var timestamp) && timestamp < oneSecondAgo)
            {
                _packetReceivedTimestamps.TryDequeue(out _);
            }

            var rtt = _pingHistory.Count > 0 ? TimeSpan.FromMilliseconds(_pingHistory.Average()) : TimeSpan.Zero;

            // TODO: Logic tính PacketLossRate cần được hoàn thiện ở UdpPeer
            if (_bitrateTimer.ElapsedMilliseconds >= 1000)
            {
                _lastSentBitrateKbps = (Interlocked.Exchange(ref _bytesSentInSecond, 0) * 8) / 1024;
                _lastReceivedBitrateKbps = (Interlocked.Exchange(ref _bytesReceivedInSecond, 0) * 8) / 1024;
                // === BẮT ĐẦU SỬA LỖI ===
                // Lấy số gói FEC khôi phục được trong 1s qua và reset bộ đếm
                _lastFecPacketsRecovered = Interlocked.Exchange(ref _fecPacketsRecovered, 0);
                // === KẾT THÚC SỬA LỖI ===

                // 2. Reset đồng hồ
                _bitrateTimer.Restart();
            }

            // --- LOGIC MỚI: TÍNH MẤT GÓI (PACKET LOSS) ---
            // === BẮT ĐẦU SỬA LỖI ===
            // Thay thế hoàn toàn logic cũ

            double packetLossRate = 0.0;
            // Tổng số gói thực nhận = số gói nhận được + số gói FEC đã cứu
            long totalPacketsInSecond = _packetReceivedTimestamps.Count + _lastFecPacketsRecovered;

            if (totalPacketsInSecond > 0)
            {
                // Tỷ lệ mất = (Số gói đã cứu) / (Tổng số gói)
                packetLossRate = (double)_lastFecPacketsRecovered / totalPacketsInSecond;
            }

            // Dọn dẹp _packetLog (vẫn giữ lại để không bị rò rỉ bộ nhớ)
            var fiveSecondsAgo = DateTime.UtcNow.Ticks - (5 * TimeSpan.TicksPerSecond);
            var keysToRemove = new System.Collections.Generic.List<uint>();
            foreach (var entry in _packetLog)
            {
                if (entry.Value.timestamp < fiveSecondsAgo)
                    keysToRemove.Add(entry.Key);
            }
            foreach (var key in keysToRemove)
                _packetLog.TryRemove(key, out _);

            // === KẾT THÚC SỬA LỖI ===

            // Trả về kết quả
            return new TelemetrySnapshot
            {
                Rtt = rtt,
                PacketsSentPerSec = _packetSentTimestamps.Count,
                PacketsReceivedPerSec = _packetReceivedTimestamps.Count,
                SentBitrateKbps = _lastSentBitrateKbps,
                ReceivedBitrateKbps = _lastReceivedBitrateKbps,
                PacketLossRate = packetLossRate * 100.0, // Chuyển sang %
                AverageLatencyMs = rtt.TotalMilliseconds
            };
        }
    }
}