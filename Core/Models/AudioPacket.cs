using System;

namespace RealTimeUdpStream.Core.Models
{
    /// <summary>
    /// Gói tin âm thanh chứa dữ liệu audio và metadata
    /// </summary>
    public class AudioPacket : IDisposable
    {
        public AudioPacketHeader Header { get; set; }
        public ArraySegment<byte> AudioData { get; }
        public bool IsFromPool { get; set; }

        private bool _disposed = false;

        public AudioPacket(AudioPacketHeader header, ArraySegment<byte> audioData)
        {
            Header = header;
            AudioData = audioData;
            IsFromPool = false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (IsFromPool && AudioData.Array != null)
                {
                    Util.BytePool.Return(AudioData.Array);
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Header chứa metadata của audio packet
    /// </summary>
    public struct AudioPacketHeader
    {
        public uint SequenceNumber;
        public ulong TimestampMs;
        public AudioCodec Codec;
        public int SampleRate;
        public int Channels;
        public int SamplesPerChannel;
        public ushort DataLength;

        public const int HeaderSize = 4 + 8 + 4 + 4 + 4 + 4 + 2; // 30 bytes

        /// <summary>
        /// Serialize header thành byte array
        /// </summary>
        public byte[] ToByteArray()
        {
            var bytes = new byte[HeaderSize];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(SequenceNumber), 0, bytes, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(TimestampMs), 0, bytes, offset, 8);
            offset += 8;
            Buffer.BlockCopy(BitConverter.GetBytes((int)Codec), 0, bytes, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(SampleRate), 0, bytes, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(Channels), 0, bytes, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(SamplesPerChannel), 0, bytes, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(DataLength), 0, bytes, offset, 2);

            return bytes;
        }

        /// <summary>
        /// Deserialize header từ byte array
        /// </summary>
        public static AudioPacketHeader FromByteArray(byte[] bytes)
        {
            if (bytes.Length < HeaderSize)
                throw new ArgumentException($"Insufficient data for header. Expected {HeaderSize} bytes, got {bytes.Length}");

            int offset = 0;
            var header = new AudioPacketHeader();

            header.SequenceNumber = BitConverter.ToUInt32(bytes, offset);
            offset += 4;
            header.TimestampMs = BitConverter.ToUInt64(bytes, offset);
            offset += 8;
            header.Codec = (AudioCodec)BitConverter.ToInt32(bytes, offset);
            offset += 4;
            header.SampleRate = BitConverter.ToInt32(bytes, offset);
            offset += 4;
            header.Channels = BitConverter.ToInt32(bytes, offset);
            offset += 4;
            header.SamplesPerChannel = BitConverter.ToInt32(bytes, offset);
            offset += 4;
            header.DataLength = BitConverter.ToUInt16(bytes, offset);

            return header;
        }
    }

    /// <summary>
    /// Các codec âm thanh được hỗ trợ
    /// </summary>
    public enum AudioCodec : byte
    {
        PCM16 = 0x01,      // 16-bit PCM
        PCM24 = 0x02,      // 24-bit PCM
        OPUS = 0x10,       // Opus codec (nén)
        AAC = 0x11         // AAC codec (nén)
    }

    /// <summary>
    /// Cấu hình âm thanh
    /// </summary>
    public class AudioConfig
    {
        public int SampleRate { get; set; } = 48000;  // 48kHz - chất lượng cao
        public int Channels { get; set; } = 2;        // Stereo
        public int BitsPerSample { get; set; } = 16;  // 16-bit
        public AudioCodec Codec { get; set; } = AudioCodec.PCM16;
        public int BufferSize { get; set; } = 1024;   // Samples per buffer

        /// <summary>
        /// Tính kích thước buffer theo bytes
        /// </summary>
        public int BufferSizeBytes => BufferSize * Channels * (BitsPerSample / 8);

        /// <summary>
        /// Thời gian của một buffer (ms)
        /// </summary>
        public double BufferDurationMs => (double)BufferSize / SampleRate * 1000;

        /// <summary>
        /// Tạo cấu hình mặc định cho audio streaming
        /// </summary>
        public static AudioConfig CreateDefault()
        {
            return new AudioConfig
            {
                SampleRate = 44100,
                Channels = 2,
                BitsPerSample = 16,
                Codec = AudioCodec.PCM16,
                BufferSize = 1024
            };
        }
    }
}