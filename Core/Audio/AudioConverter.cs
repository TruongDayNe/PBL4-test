using System;
using NAudio.Wave;

namespace RealTimeUdpStream.Core.Audio
{
    /// <summary>
    /// Simple audio format converter to ensure consistent format between system audio and microphone
    /// Fixes pitch shift and quality issues caused by format mismatch
    /// </summary>
    public class AudioConverter : IDisposable
    {
        private readonly WaveFormat _targetFormat;
        private bool _disposed = false;

        /// <summary>
        /// Initialize converter to target format (usually 48kHz 16-bit stereo like microphone)
        /// </summary>
        /// <param name="targetFormat">Target wave format</param>
        public AudioConverter(WaveFormat targetFormat)
        {
            _targetFormat = targetFormat ?? throw new ArgumentNullException(nameof(targetFormat));
        }

        /// <summary>
        /// Convert audio data from source format to target format
        /// </summary>
        /// <param name="sourceFormat">Source audio format</param>
        /// <param name="audioData">Audio data to convert</param>
        /// <param name="bytesRecorded">Number of bytes in audio data</param>
        /// <returns>Converted audio data</returns>
        public byte[] ConvertAudio(WaveFormat sourceFormat, byte[] audioData, int bytesRecorded)
        {
            if (sourceFormat == null || audioData == null || bytesRecorded <= 0)
                return audioData;

            // If formats match exactly, no conversion needed
            if (FormatsMatch(sourceFormat, _targetFormat))
            {
                Console.WriteLine("[AudioConverter] Formats match - no conversion needed");
                return audioData;
            }

            try
            {
                Console.WriteLine($"[AudioConverter] Converting: {sourceFormat} → {_targetFormat}");
                
                // CRITICAL: Only handle float32→int16 conversion for same sample rate
                if (sourceFormat.Encoding == WaveFormatEncoding.IeeeFloat && 
                    sourceFormat.BitsPerSample == 32 &&
                    _targetFormat.Encoding == WaveFormatEncoding.Pcm &&
                    _targetFormat.BitsPerSample == 16 &&
                    sourceFormat.SampleRate == _targetFormat.SampleRate) // Same sample rate!
                {
                    Console.WriteLine("[AudioConverter] Using DIRECT float32→int16 conversion (no resampling)");
                    return ConvertFloat32ToInt16(audioData, bytesRecorded, sourceFormat.Channels, _targetFormat.Channels);
                }
                
                // For different sample rates or complex conversions, use NAudio
                Console.WriteLine("[AudioConverter] Using NAudio WaveFormatConversionStream");
                using (var sourceStream = new RawSourceWaveStream(audioData, 0, bytesRecorded, sourceFormat))
                using (var conversionStream = new WaveFormatConversionStream(_targetFormat, sourceStream))
                {
                    byte[] convertedData = new byte[bytesRecorded * 4]; // Extra space for safety
                    int convertedBytes = conversionStream.Read(convertedData, 0, convertedData.Length);
                    
                    if (convertedBytes <= 0)
                    {
                        Console.WriteLine("[AudioConverter] WARNING: Conversion produced 0 bytes!");
                        return audioData;
                    }
                    
                    // Trim to actual size
                    byte[] result = new byte[convertedBytes];
                    Array.Copy(convertedData, result, convertedBytes);
                    Console.WriteLine($"[AudioConverter] Conversion successful: {bytesRecorded} → {convertedBytes} bytes");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioConverter] ERROR: Conversion failed - {ex.Message}");
                Console.WriteLine($"[AudioConverter] Returning original audio data");
                return audioData; // Return original data if conversion fails
            }
        }

        /// <summary>
        /// Convert 32-bit float audio to 16-bit integer
        /// This fixes the pitch shift and quality issues caused by format mismatch
        /// </summary>
        private byte[] ConvertFloat32ToInt16(byte[] floatData, int bytesRecorded, int sourceChannels, int targetChannels)
        {
            int floatSampleCount = bytesRecorded / 4; // 4 bytes per 32-bit float sample
            
            // Calculate output size
            int outputSampleCount = floatSampleCount;
            if (sourceChannels == 1 && targetChannels == 2)
            {
                outputSampleCount = floatSampleCount * 2; // Duplicate mono to stereo
            }
            else if (sourceChannels == 2 && targetChannels == 1)
            {
                outputSampleCount = floatSampleCount / 2; // Mix stereo to mono
            }
            
            byte[] result = new byte[outputSampleCount * 2]; // 2 bytes per 16-bit sample
            
            if (sourceChannels == targetChannels)
            {
                // Same channel count: direct conversion
                for (int i = 0; i < floatSampleCount; i++)
                {
                    // Read 32-bit float sample
                    float floatSample = BitConverter.ToSingle(floatData, i * 4);
                    
                    // Clamp to [-1.0, 1.0] range to prevent clipping/distortion
                    floatSample = Math.Max(-1.0f, Math.Min(1.0f, floatSample));
                    
                    // Convert to 16-bit signed integer (±32767)
                    short intSample = (short)(floatSample * 32767);
                    
                    // Write 16-bit sample (little-endian)
                    int byteIndex = i * 2;
                    result[byteIndex] = (byte)(intSample & 0xFF);
                    result[byteIndex + 1] = (byte)((intSample >> 8) & 0xFF);
                }
            }
            else if (sourceChannels == 1 && targetChannels == 2)
            {
                // Mono to Stereo: duplicate each sample
                for (int i = 0; i < floatSampleCount; i++)
                {
                    float floatSample = BitConverter.ToSingle(floatData, i * 4);
                    floatSample = Math.Max(-1.0f, Math.Min(1.0f, floatSample));
                    short intSample = (short)(floatSample * 32767);
                    
                    int byteIndex = i * 4; // Each output frame is 4 bytes (L+R channels)
                    // Left channel
                    result[byteIndex] = (byte)(intSample & 0xFF);
                    result[byteIndex + 1] = (byte)((intSample >> 8) & 0xFF);
                    // Right channel (duplicate)
                    result[byteIndex + 2] = result[byteIndex];
                    result[byteIndex + 3] = result[byteIndex + 1];
                }
            }
            else if (sourceChannels == 2 && targetChannels == 1)
            {
                // Stereo to Mono: average L+R channels
                for (int i = 0; i < floatSampleCount; i += 2)
                {
                    float left = BitConverter.ToSingle(floatData, i * 4);
                    float right = BitConverter.ToSingle(floatData, (i + 1) * 4);
                    float mono = (left + right) / 2.0f;
                    mono = Math.Max(-1.0f, Math.Min(1.0f, mono));
                    short intSample = (short)(mono * 32767);
                    
                    int byteIndex = (i / 2) * 2;
                    result[byteIndex] = (byte)(intSample & 0xFF);
                    result[byteIndex + 1] = (byte)((intSample >> 8) & 0xFF);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Check if two wave formats are equivalent
        /// </summary>
        private bool FormatsMatch(WaveFormat format1, WaveFormat format2)
        {
            return format1.SampleRate == format2.SampleRate &&
                   format1.Channels == format2.Channels &&
                   format1.BitsPerSample == format2.BitsPerSample &&
                   format1.Encoding == format2.Encoding;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
