using System;

public class TelemetrySnapshot
{
    public TimeSpan Rtt { get; set; }
    public int PacketsSentPerSec { get; set; }
    public int PacketsReceivedPerSec { get; set; }
    public double PacketLossRate { get; set; }
    public long SentBitrateKbps { get; set; }
    public long ReceivedBitrateKbps { get; set; }
    public double AverageLatencyMs { get; set; }

}