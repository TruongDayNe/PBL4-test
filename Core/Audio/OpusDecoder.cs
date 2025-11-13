using System;
using Concentus.Structs;

namespace RealTimeUdpStream.Core.Audio
{
    /// <summary>
    /// Wrapper cho Opus decoder để giải nén audio từ Opus sang PCM
    /// </summary>
    public class OpusDecoder : IDisposable
    {
        private Concentus.Structs.OpusDecoder _decoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _frameSize;
        
        /// <summary>
        /// Tạo Opus decoder
        /// </summary>
        /// <param name="sampleRate">Sample rate (8000, 12000, 16000, 24000, 48000)</param>
        /// <param name="channels">Số kênh (1=mono, 2=stereo)</param>
        public OpusDecoder(int sampleRate = 48000, int channels = 2)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            
            // Frame size: 20ms frames
            // 48000 Hz * 0.02s = 960 samples per frame
            _frameSize = sampleRate / 50; // 20ms = 1/50 second
            
            // Tạo decoder
            _decoder = new Concentus.Structs.OpusDecoder(sampleRate, channels);
        }
        
        /// <summary>
        /// Decode Opus data sang PCM format
        /// </summary>
        /// <param name="opusData">Opus encoded data</param>
        /// <returns>PCM data (16-bit little-endian interleaved)</returns>
        public byte[] Decode(byte[] opusData)
        {
            if (opusData == null || opusData.Length == 0)
                return null;
                
            // Prepare output buffer for decoded PCM
            // Một frame Opus = 20ms @ 48kHz stereo = 960 samples/channel * 2 channels = 1920 samples
            int maxSamplesPerFrame = _frameSize * _channels;
            short[] pcmSamples = new short[maxSamplesPerFrame * 10]; // Buffer cho nhiều frames
            
            int totalDecodedSamples = 0;
            int opusOffset = 0;
            
            // Decode Opus packets
            // Mỗi packet có thể chứa 1 hoặc nhiều frames
            while (opusOffset < opusData.Length)
            {
                // Tìm packet size (cần parse Opus packet header hoặc estimate)
                // Đơn giản: giả sử toàn bộ data là 1 packet
                int packetSize = opusData.Length - opusOffset;
                
                // Decode packet
                int decodedSamples = _decoder.Decode(
                    opusData,
                    opusOffset,
                    packetSize,
                    pcmSamples,
                    totalDecodedSamples,
                    maxSamplesPerFrame,
                    false // decode FEC (forward error correction)
                );
                
                if (decodedSamples < 0)
                    throw new Exception($"Opus decoding failed with error code: {decodedSamples}");
                    
                totalDecodedSamples += decodedSamples;
                opusOffset += packetSize; // Đơn giản: 1 packet = toàn bộ data
                
                // Nếu buffer sắp đầy, resize
                if (totalDecodedSamples + maxSamplesPerFrame > pcmSamples.Length)
                {
                    Array.Resize(ref pcmSamples, pcmSamples.Length * 2);
                }
            }
            
            // Convert short[] to byte[] (PCM16 = 16-bit samples)
            byte[] pcmData = new byte[totalDecodedSamples * 2];
            Buffer.BlockCopy(pcmSamples, 0, pcmData, 0, pcmData.Length);
            
            return pcmData;
        }
        
        /// <summary>
        /// Decode một frame Opus với kích thước cụ thể
        /// </summary>
        /// <param name="opusData">Opus frame data</param>
        /// <param name="frameSize">Frame size in bytes</param>
        /// <returns>PCM data</returns>
        public byte[] DecodeFrame(byte[] opusData, int frameSize)
        {
            if (opusData == null || frameSize <= 0)
                return null;
                
            int samplesPerFrame = _frameSize * _channels;
            short[] pcmSamples = new short[samplesPerFrame];
            
            int decodedSamples = _decoder.Decode(
                opusData,
                0,
                frameSize,
                pcmSamples,
                0,
                _frameSize,
                false
            );
            
            if (decodedSamples < 0)
                throw new Exception($"Opus frame decoding failed with error code: {decodedSamples}");
                
            byte[] pcmData = new byte[decodedSamples * 2];
            Buffer.BlockCopy(pcmSamples, 0, pcmData, 0, pcmData.Length);
            
            return pcmData;
        }
        
        /// <summary>
        /// Get frame size in samples (per channel)
        /// </summary>
        public int FrameSize => _frameSize;
        
        /// <summary>
        /// Get frame duration in milliseconds
        /// </summary>
        public int FrameDurationMs => _frameSize * 1000 / _sampleRate;
        
        public void Dispose()
        {
            _decoder = null;
        }
    }
}
