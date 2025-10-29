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

            // If formats match, no conversion needed
            if (FormatsMatch(sourceFormat, _targetFormat))
            {
                return audioData;
            }

            try
            {
                // Simple conversion for basic format differences
                // This handles the most common case: 32-bit float to 16-bit int
                if (sourceFormat.Encoding == WaveFormatEncoding.IeeeFloat && 
                    sourceFormat.BitsPerSample == 32 &&
                    _targetFormat.Encoding == WaveFormatEncoding.Pcm &&
                    _targetFormat.BitsPerSample == 16)
                {
                    return ConvertFloat32ToInt16(audioData, bytesRecorded, sourceFormat.Channels, _targetFormat.Channels);
                }
                
                // For complex conversions, use NAudio's built-in conversion
                using (var sourceStream = new RawSourceWaveStream(audioData, 0, bytesRecorded, sourceFormat))
                using (var conversionStream = new WaveFormatConversionStream(_targetFormat, sourceStream))
                {
                    byte[] convertedData = new byte[bytesRecorded * 2]; // Allocate extra space
                    int convertedBytes = conversionStream.Read(convertedData, 0, convertedData.Length);
                    
                    // Trim to actual size
                    byte[] result = new byte[convertedBytes];
                    Array.Copy(convertedData, result, convertedBytes);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio conversion failed: {ex.Message}");
                return audioData; // Return original data if conversion fails
            }
        }

        /// <summary>
        /// Convert 32-bit float audio to 16-bit integer
        /// This fixes the pitch shift and quality issues
        /// </summary>
        private byte[] ConvertFloat32ToInt16(byte[] floatData, int bytesRecorded, int sourceChannels, int targetChannels)
        {
            int sampleCount = bytesRecorded / 4; // 4 bytes per 32-bit float sample
            int targetSampleCount = (sourceChannels == targetChannels) ? sampleCount : 
                                  (targetChannels == 2 && sourceChannels == 1) ? sampleCount * 2 : sampleCount;
            
            byte[] result = new byte[targetSampleCount * 2]; // 2 bytes per 16-bit sample
            
            for (int i = 0, j = 0; i < sampleCount && j < result.Length - 1; i++, j += 2)
            {
                // Read 32-bit float sample
                float floatSample = BitConverter.ToSingle(floatData, i * 4);
                
                // Clamp to [-1.0, 1.0] range to prevent overflow
                floatSample = Math.Max(-1.0f, Math.Min(1.0f, floatSample));
                
                // Convert to 16-bit signed integer
                short intSample = (short)(floatSample * 32767);
                
                // Write 16-bit sample (little-endian)
                result[j] = (byte)(intSample & 0xFF);
                result[j + 1] = (byte)((intSample >> 8) & 0xFF);
                
                // Handle mono to stereo conversion
                if (sourceChannels == 1 && targetChannels == 2 && j + 3 < result.Length)
                {
                    result[j + 2] = result[j];     // Duplicate to right channel
                    result[j + 3] = result[j + 1];
                    j += 2; // Skip the duplicated sample
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
