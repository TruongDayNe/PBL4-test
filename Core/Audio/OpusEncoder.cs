using System;
using Concentus.Structs;
using Concentus.Enums;

namespace RealTimeUdpStream.Core.Audio
{
    /// <summary>
    /// Wrapper cho Opus encoder để nén audio từ PCM sang Opus
    /// </summary>
    public class OpusEncoder : IDisposable
    {
        private Concentus.Structs.OpusEncoder _encoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _frameSize;
        private readonly int _bitrate;
        
        /// <summary>
        /// Tạo Opus encoder
        /// </summary>
        /// <param name="sampleRate">Sample rate (8000, 12000, 16000, 24000, 48000)</param>
        /// <param name="channels">Số kênh (1=mono, 2=stereo)</param>
        /// <param name="bitrate">Bitrate mong muốn (bps). Default 96000 = 96 Kbps</param>
        /// <param name="application">Application type. Default Audio</param>
        public OpusEncoder(int sampleRate = 48000, int channels = 2, int bitrate = 96000, OpusApplication application = OpusApplication.OPUS_APPLICATION_AUDIO)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _bitrate = bitrate;
            
            // Frame size: 20ms frames
            // 48000 Hz * 0.02s = 960 samples per frame
            _frameSize = sampleRate / 50; // 20ms = 1/50 second
            
            // Tạo encoder
            _encoder = new Concentus.Structs.OpusEncoder(sampleRate, channels, application);
            
            // Configure encoder
            _encoder.Bitrate = bitrate;
            _encoder.Complexity = 10; // 0-10, 10 = best quality
            _encoder.SignalType = OpusSignal.OPUS_SIGNAL_MUSIC; // Optimize for music/audio
            _encoder.UseVBR = true; // Variable bitrate
            _encoder.UseConstrainedVBR = true; // Constrained VBR (more predictable size)
        }
        
        /// <summary>
        /// Encode PCM data sang Opus format
        /// </summary>
        /// <param name="pcmData">PCM data (16-bit little-endian interleaved)</param>
        /// <returns>Opus encoded data</returns>
        public byte[] Encode(byte[] pcmData)
        {
            if (pcmData == null || pcmData.Length == 0)
                return null;
                
            // Convert byte[] to short[] (PCM16 = 16-bit samples)
            int sampleCount = pcmData.Length / 2; // 2 bytes per sample
            short[] pcmSamples = new short[sampleCount];
            Buffer.BlockCopy(pcmData, 0, pcmSamples, 0, pcmData.Length);
            
            // Calculate số frames cần encode
            int samplesPerFrame = _frameSize * _channels; // stereo: 960*2 = 1920 samples per frame
            int frameCount = sampleCount / samplesPerFrame;
            
            if (frameCount == 0)
            {
                // Nếu data nhỏ hơn 1 frame, pad thêm 0
                Array.Resize(ref pcmSamples, samplesPerFrame);
                frameCount = 1;
            }
            
            // Prepare output buffer (Opus frame tối đa ~1500 bytes cho 20ms @ 96kbps)
            byte[] opusData = new byte[frameCount * 1500];
            int totalEncodedBytes = 0;
            
            // Encode từng frame
            for (int i = 0; i < frameCount; i++)
            {
                int offset = i * samplesPerFrame;
                int encodedBytes = _encoder.Encode(
                    pcmSamples, 
                    offset, 
                    _frameSize, 
                    opusData, 
                    totalEncodedBytes, 
                    opusData.Length - totalEncodedBytes
                );
                
                if (encodedBytes < 0)
                    throw new Exception($"Opus encoding failed with error code: {encodedBytes}");
                    
                totalEncodedBytes += encodedBytes;
            }
            
            // Resize array về đúng kích thước đã encode
            Array.Resize(ref opusData, totalEncodedBytes);
            return opusData;
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
            _encoder = null;
        }
    }
}
