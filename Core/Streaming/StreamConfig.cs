namespace Core.Streaming
{
    /// <summary>
    /// Đối tượng chứa cấu hình stream, có thể được serialize/deserialize.
    /// </summary>
    public class StreamConfig
    {
        public string VideoCodec { get; set; } = "libx264";
        public string Preset { get; set; } = "ultrafast";
        public int Crf { get; set; } = 28;
        public string PixelFormat { get; set; } = "yuv420p";
        public int FrameRate { get; set; } = 30;
        public string OutputFormat { get; set; } = "mpegts"; // 'mpegts' là lý tưởng cho UDP
    }
}