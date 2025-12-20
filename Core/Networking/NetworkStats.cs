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

        // FEC recovered
        private long _fecPacketsRecovered = 0;
        private long _lastFecPacketsRecovered = 0;

        // Packet Loss Counter (MỚI)
        private long _packetsLost = 0;
        private long _lastPacketsLost = 0;

        //log để tính mất gói (giữ lại để tương thích, nhưng không dùng chính cho loss rate)
        private readonly ConcurrentDictionary<uint, (long timestamp, bool acked)> _packetLog = new ConcurrentDictionary<uint, (long, bool)>();

        // --- BIẾN MỚI CHO BITRATE ---
        private long _bytesSentInSecond = 0;
        private long _bytesReceivedInSecond = 0;
        private long _lastSentBitrateKbps = 0;
        private long _lastReceivedBitrateKbps = 0;

        private readonly Stopwatch _bitrateTimer = Stopwatch.StartNew();

        public void UpdateRtt(long sentTimestampMs)
        {
            long now = (long)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            long rttMs = now - sentTimestampMs;

            // Chỉ chấp nhận RTT dương và hợp lý (dưới 5 giây)
            if (rttMs >= 0 && rttMs < 5000)
            {
                _pingHistory.Enqueue(rttMs);
                while (_pingHistory.Count > 10)
                {
                    _pingHistory.TryDequeue(out _);
                }
            }
        }

        public void LogFecPacketRecovered()
        {
            Interlocked.Increment(ref _fecPacketsRecovered);
        }

        // HÀM MỚI: Ghi nhận số gói bị mất do phát hiện hổng Sequence
        public void LogLoss(int count)
        {
            Interlocked.Add(ref _packetsLost, count);
        }

        public void LogPacketSent(uint sequenceNumber, int size)
        {
            _packetSentTimestamps.Enqueue(DateTime.UtcNow);
            Interlocked.Add(ref _bytesSentInSecond, size);
        }

        public void LogPacketReceived(int size)
        {
            _packetReceivedTimestamps.Enqueue(DateTime.UtcNow);
            Interlocked.Add(ref _bytesReceivedInSecond, size);
        }

        public TelemetrySnapshot GetSnapshot()
        {
            // Dọn dẹp queue cũ
            var oneSecondAgo = DateTime.UtcNow.AddSeconds(-1);
            while (_packetSentTimestamps.TryPeek(out var timestamp) && timestamp < oneSecondAgo)
                _packetSentTimestamps.TryDequeue(out _);
            while (_packetReceivedTimestamps.TryPeek(out var timestamp) && timestamp < oneSecondAgo)
                _packetReceivedTimestamps.TryDequeue(out _);

            // Tính RTT trung bình
            var rtt = _pingHistory.Count > 0 ? TimeSpan.FromMilliseconds(_pingHistory.Average()) : TimeSpan.Zero;

            // Timer 1 giây để chốt số liệu Bitrate và Loss
            if (_bitrateTimer.ElapsedMilliseconds >= 1000)
            {
                _lastSentBitrateKbps = (Interlocked.Exchange(ref _bytesSentInSecond, 0) * 8) / 1024;
                _lastReceivedBitrateKbps = (Interlocked.Exchange(ref _bytesReceivedInSecond, 0) * 8) / 1024;

                // Lấy số liệu loss/recovered trong 1s qua và reset
                _lastFecPacketsRecovered = Interlocked.Exchange(ref _fecPacketsRecovered, 0);
                _lastPacketsLost = Interlocked.Exchange(ref _packetsLost, 0);

                _bitrateTimer.Restart();
            }

            // --- TÍNH TOÁN PACKET LOSS RATE ---
            double packetLossRate = 0.0;

            // Tổng số gói lẽ ra phải nhận = Thực nhận + Đã cứu (FEC) + Đã mất hẳn (Loss Gap)
            long receivedCount = _packetReceivedTimestamps.Count;
            long totalExpected = receivedCount + _lastPacketsLost; // FEC Recovered đã tính là "nhận được" trong logic FEC rồi hoặc tách riêng tùy ý

            // Để chính xác:
            // Loss Rate = (Lost + Recovered) / (Received + Lost + Recovered) 
            // Hoặc nếu coi Recovered là thành công thì: Loss Rate = Lost / (Received + Lost)
            // Ở đây ta tính tổng thể chất lượng mạng:
            long totalEvents = receivedCount + _lastPacketsLost + _lastFecPacketsRecovered;

            if (totalEvents > 0)
            {
                // Tỷ lệ gói bị lỗi trên đường truyền (bao gồm cả gói cứu được và mất hẳn)
                packetLossRate = (double)(_lastPacketsLost + _lastFecPacketsRecovered) / totalEvents;
            }

            return new TelemetrySnapshot
            {
                Rtt = rtt,
                PacketsSentPerSec = _packetSentTimestamps.Count,
                PacketsReceivedPerSec = (int)receivedCount,
                SentBitrateKbps = _lastSentBitrateKbps,
                ReceivedBitrateKbps = _lastReceivedBitrateKbps,
                PacketLossRate = packetLossRate * 100.0,
                AverageLatencyMs = rtt.TotalMilliseconds
            };
        }
    }
}