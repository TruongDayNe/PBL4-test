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
                // 1. Tính toán
                // (Số byte * 8) -> ra bit. Chia 1024 -> ra Kilobit.
                // Interlocked.Exchange trả về giá trị cũ VÀ reset nó về 0
                _lastSentBitrateKbps = (Interlocked.Exchange(ref _bytesSentInSecond, 0) * 8) / 1024;
                _lastReceivedBitrateKbps = (Interlocked.Exchange(ref _bytesReceivedInSecond, 0) * 8) / 1024;

                // 2. Reset đồng hồ
                _bitrateTimer.Restart();
            }

            // --- LOGIC MỚI: TÍNH MẤT GÓI (PACKET LOSS) ---
            double packetLossRate = 0.0;
            long totalSent = _packetLog.Count;
            long totalAcked = 0;

            // Dọn dẹp log, xóa các gói tin cũ hơn 5 giây
            var fiveSecondsAgo = DateTime.UtcNow.Ticks - (5 * TimeSpan.TicksPerSecond);
            var keysToRemove = new System.Collections.Generic.List<uint>();

            foreach (var entry in _packetLog)
            {
                if (entry.Value.timestamp < fiveSecondsAgo)
                {
                    // Nếu quá cũ, đánh dấu để xóa
                    keysToRemove.Add(entry.Key);
                }
                else if (entry.Value.acked)
                {
                    // Nếu đã được ACK, đếm
                    totalAcked++;
                }
            }

            // Thực hiện xóa
            foreach (var key in keysToRemove)
            {
                _packetLog.TryRemove(key, out _);
            }

            // Tính toán tỷ lệ
            if (totalSent > 0)
            {
                // Tỷ lệ mất = (Tổng gửi - Tổng nhận) / Tổng gửi
                // (Chỉ tính các gói trong 5 giây qua)
                long relevantSent = totalSent - (keysToRemove.Count - totalAcked); // 
                long relevantAcked = totalAcked;
                if (relevantSent > 0)
                {
                    packetLossRate = (double)(relevantSent - relevantAcked) / relevantSent;
                }
            }

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