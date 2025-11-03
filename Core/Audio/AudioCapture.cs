using NAudio.Wave;
using RealTimeUdpStream.Core.Models;
using System;
using System.Diagnostics;
using System.IO;

namespace RealTimeUdpStream.Core.Audio
{
    /// <summary>
    /// Capture âm thanh từ microphone hoặc system audio với format conversion
    /// </summary>
    public class AudioCapture : IDisposable
    {
        private readonly AudioConfig _config;
        private IWaveIn _waveIn;
        private WaveFormat _waveFormat;
        private WaveFormat _captureFormat; // Format thực tế của capture device
        private AudioConverter _audioConverter;
        private AudioInputType _inputType;
        private bool _isCapturing = false;
        private bool _disposed = false;

        // Standard format cho tất cả audio: 48kHz, 16-bit, stereo
        private static readonly WaveFormat StandardFormat = new WaveFormat(48000, 16, 2);

        public event Action<AudioPacket> OnAudioDataAvailable;

        public AudioCapture(AudioConfig config, AudioInputType inputType = AudioInputType.Microphone)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _inputType = inputType;
            
            // Initialize converter to standard format
            _audioConverter = new AudioConverter(StandardFormat);
            
            InitializeAudioInput(inputType);
        }

        private void InitializeAudioInput(AudioInputType inputType)
        {
            // Use standard format for microphone
            _waveFormat = StandardFormat;

            switch (inputType)
            {
                case AudioInputType.Microphone:
                    _waveIn = new WaveInEvent
                    {
                        WaveFormat = _waveFormat,
                        BufferMilliseconds = (int)_config.BufferDurationMs
                    };
                    _captureFormat = _waveFormat; // Microphone already uses standard format
                    Debug.WriteLine($"Microphone format: {_waveFormat} (STANDARD)");
                    break;

                case AudioInputType.SystemAudio:
                    // System audio uses loopback which may have different format
                    var loopback = new WasapiLoopbackCapture();
                    _waveIn = loopback;
                    _captureFormat = loopback.WaveFormat; // May be 32-bit float
                    
                    Console.WriteLine($"========== SYSTEM AUDIO CAPTURE INITIALIZED ==========");
                    Console.WriteLine($"Capture format: {_captureFormat}");
                    Console.WriteLine($"  - SampleRate: {_captureFormat.SampleRate} Hz");
                    Console.WriteLine($"  - Channels: {_captureFormat.Channels}");
                    Console.WriteLine($"  - BitsPerSample: {_captureFormat.BitsPerSample}");
                    Console.WriteLine($"  - Encoding: {_captureFormat.Encoding}");
                    Console.WriteLine($"Target format: {StandardFormat}");
                    Console.WriteLine($"  - SampleRate: {StandardFormat.SampleRate} Hz");
                    Console.WriteLine($"  - Channels: {StandardFormat.Channels}");
                    Console.WriteLine($"  - BitsPerSample: {StandardFormat.BitsPerSample}");
                    Console.WriteLine($"  - Encoding: {StandardFormat.Encoding}");
                    
                    if (_captureFormat.Encoding == WaveFormatEncoding.IeeeFloat && _captureFormat.BitsPerSample == 32)
                    {
                        Console.WriteLine("⚠️  32-bit float detected - WILL CONVERT to 16-bit PCM");
                    }
                    else
                    {
                        Console.WriteLine("✓ Format compatible");
                    }
                    Console.WriteLine($"====================================================");
                    break;

                default:
                    throw new ArgumentException($"Unsupported input type: {inputType}");
            }

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
        }

        public void StartCapture()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioCapture));

            if (_isCapturing) return;

            try
            {
                Debug.WriteLine($"[StartCapture] About to call _waveIn.StartRecording()");
                
                _waveIn.StartRecording();
                _isCapturing = true;
                
                Debug.WriteLine($"[StartCapture] Recording started! Format={_waveFormat}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartCapture] ERROR: {ex.Message}");
                throw;
            }
        }

        public void StopCapture()
        {
            Debug.WriteLine($"🛑 StopCapture CALLED! _isCapturing={_isCapturing}");
            
            if (!_isCapturing) return;

            try
            {
                _waveIn.StopRecording();
                _isCapturing = false;
                Debug.WriteLine("✓ Recording stopped successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error stopping: {ex.Message}");
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;
            
            if (!_isCapturing)
            {
                Debug.WriteLine($"⚠️ OnDataAvailable SKIPPED: _isCapturing=false!");
                return;
            }

            try
            {
                byte[] audioData = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, audioData, 0, e.BytesRecorded);

                // Convert System Audio 32-bit float → 16-bit PCM if needed
                if (_inputType == AudioInputType.SystemAudio && 
                    _captureFormat.Encoding == WaveFormatEncoding.IeeeFloat && 
                    _captureFormat.BitsPerSample == 32)
                {
                    // Manual conversion: 32-bit float → 16-bit PCM
                    int sampleCount = e.BytesRecorded / 4; // Each float is 4 bytes
                    byte[] convertedData = new byte[sampleCount * 2]; // Each int16 is 2 bytes
                    
                    for (int i = 0; i < sampleCount; i++)
                    {
                        // Read float sample (little-endian by default on x86/x64)
                        float floatSample = BitConverter.ToSingle(audioData, i * 4);
                        
                        // Clamp to [-1.0, 1.0] to prevent clipping
                        if (floatSample > 1.0f) floatSample = 1.0f;
                        if (floatSample < -1.0f) floatSample = -1.0f;
                        
                        // Convert to 16-bit PCM: scale by 32767 and round
                        // NOTE: Removed dithering as it may introduce noise
                        float scaledFloat = floatSample * 32767f;
                        int scaledInt = (int)Math.Round(scaledFloat);
                        
                        // Ensure within int16 range (should already be after clamp, but be safe)
                        if (scaledInt > 32767) scaledInt = 32767;
                        if (scaledInt < -32768) scaledInt = -32768;
                        
                        short int16Sample = (short)scaledInt;
                        
                        // Write to output buffer (little-endian)
                        byte[] int16Bytes = BitConverter.GetBytes(int16Sample);
                        convertedData[i * 2] = int16Bytes[0];
                        convertedData[i * 2 + 1] = int16Bytes[1];
                    }
                    
                    audioData = convertedData;
                    // Console.WriteLine($"✓ Converted {sampleCount} samples from float32 to int16"); // TAT LOG
                }
                
                // Check sample rate (System Audio might be 44100Hz, need 48000Hz)
                if (_inputType == AudioInputType.SystemAudio && _captureFormat.SampleRate != StandardFormat.SampleRate)
                {
                    // Console.WriteLine($"⚠️ SAMPLE RATE MISMATCH! Captured: {_captureFormat.SampleRate}Hz, Target: {StandardFormat.SampleRate}Hz"); // TAT LOG
                    Console.WriteLine($"⚠️ THIS WILL CAUSE PITCH SHIFT! Need resampling!");
                    // TODO: Add resampling
                }
                
                var header = new AudioPacketHeader
                {
                    SequenceNumber = GenerateSequenceNumber(),
                    TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond),
                    Codec = _config.Codec,
                    SampleRate = StandardFormat.SampleRate, // Always 48000Hz after conversion
                    Channels = StandardFormat.Channels,     // Always 2 channels
                    SamplesPerChannel = audioData.Length / (StandardFormat.Channels * (StandardFormat.BitsPerSample / 8)),
                    DataLength = (ushort)audioData.Length
                };

                var packet = new AudioPacket(header, new ArraySegment<byte>(audioData));
                OnAudioDataAvailable?.Invoke(packet);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ ERROR in OnDataAvailable: {ex.Message}");
            }
        }

        private bool FormatsMatch(WaveFormat format1, WaveFormat format2)
        {
            return format1.SampleRate == format2.SampleRate &&
                   format1.Channels == format2.Channels &&
                   format1.BitsPerSample == format2.BitsPerSample &&
                   format1.Encoding == format2.Encoding;
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            Debug.WriteLine($"⛔ OnRecordingStopped FIRED! Exception={e.Exception?.Message ?? "NONE"}");
            _isCapturing = false;
        }

        private uint _sequenceNumber = 0;
        private uint GenerateSequenceNumber() => ++_sequenceNumber;

        public bool IsCapturing => _isCapturing;

        public void Dispose()
        {
            if (_disposed) return;

            StopCapture();
            _waveIn?.Dispose();
            _audioConverter?.Dispose();
            _disposed = true;

            Debug.WriteLine("AudioCapture disposed");
        }
    }

    public enum AudioInputType
    {
        Microphone,
        SystemAudio
    }
}