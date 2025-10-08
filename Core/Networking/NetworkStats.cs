using System;
using System.Collections.Concurrent;
using System.Linq;

namespace RealTimeUdpStream.Core.Networking
{
    public class NetworkStats
    {
        private readonly ConcurrentQueue<long> _pingHistory = new ConcurrentQueue<long>();
        private readonly ConcurrentQueue<DateTime> _packetSentTimestamps = new ConcurrentQueue<DateTime>();
        private readonly ConcurrentQueue<DateTime> _packetReceivedTimestamps = new ConcurrentQueue<DateTime>();
        private readonly ConcurrentDictionary<uint, (long timestamp, bool acked)> _packetLog = new ConcurrentDictionary<uint, (long, bool)>();
        private readonly object _lock = new object();

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

        public void LogPacketSent(uint sequenceNumber)
        {
            _packetSentTimestamps.Enqueue(DateTime.UtcNow);
            _packetLog.TryAdd(sequenceNumber, (DateTime.UtcNow.Ticks, false));
        }

        public void LogPacketReceived()
        {
            _packetReceivedTimestamps.Enqueue(DateTime.UtcNow);
        }

        public void LogPacketAcked(uint sequenceNumber)
        {
            if (_packetLog.ContainsKey(sequenceNumber))
            {
                _packetLog[sequenceNumber] = (_packetLog[sequenceNumber].timestamp, true);
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
            return new TelemetrySnapshot
            {
                Rtt = rtt,
                PacketsSentPerSec = _packetSentTimestamps.Count,
                PacketsReceivedPerSec = _packetReceivedTimestamps.Count,
                //PacketLossRate = ...,
                //CurrentBitrateKbps = ...
            };
        }
    }
}