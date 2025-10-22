using System;

public class TelemetrySnapshot
{
    public TimeSpan Rtt { get; set; }
    public int PacketsSentPerSec { get; set; }
    public int PacketsReceivedPerSec { get; set; }
    public double PacketLossRate { get; set; }
    public long CurrentBitrateKbps { get; set; }
    public double AverageLatencyMs { get; set; }

}