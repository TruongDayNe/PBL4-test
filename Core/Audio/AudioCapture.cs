using NAudio.Wave;
using RealTimeUdpStream.Core.Models;
using System;
using System.Diagnostics;

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
                    
                    Debug.WriteLine($"System audio capture initialized");
                    Debug.WriteLine($"Capture format: {_captureFormat}");
                    Debug.WriteLine($"Will convert to: {StandardFormat}");
                    
                    if (_captureFormat.Encoding == WaveFormatEncoding.IeeeFloat && _captureFormat.BitsPerSample == 32)
                    {
                        Debug.WriteLine("⚠️  32-bit float detected - will convert to 16-bit PCM to fix quality issues");
                    }
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
                _waveIn.StartRecording();
                _isCapturing = true;
                Debug.WriteLine($"Audio capture started - {_waveFormat}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start audio capture: {ex.Message}");
                throw;
            }
        }

        public void StopCapture()
        {
            if (!_isCapturing) return;

            try
            {
                _waveIn.StopRecording();
                _isCapturing = false;
                Debug.WriteLine("Audio capture stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping audio capture: {ex.Message}");
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            Debug.WriteLine($"[AudioCapture] OnDataAvailable: {e.BytesRecorded} bytes");

            try
            {
                byte[] audioData = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, audioData, 0, e.BytesRecorded);

                // Convert system audio to standard format if needed
                if (_inputType == AudioInputType.SystemAudio && !FormatsMatch(_captureFormat, StandardFormat))
                {
                    audioData = _audioConverter.ConvertAudio(_captureFormat, audioData, e.BytesRecorded);
                    Debug.WriteLine($"Converted: {_captureFormat} → {StandardFormat}");
                }

                var header = new AudioPacketHeader
                {
                    SequenceNumber = GenerateSequenceNumber(),
                    TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond),
                    Codec = _config.Codec,
                    SampleRate = StandardFormat.SampleRate, // Always use standard format
                    Channels = StandardFormat.Channels,
                    SamplesPerChannel = audioData.Length / (StandardFormat.Channels * (StandardFormat.BitsPerSample / 8)),
                    DataLength = (ushort)audioData.Length
                };

                var packet = new AudioPacket(header, new ArraySegment<byte>(audioData));
                OnAudioDataAvailable?.Invoke(packet);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing audio data: {ex.Message}");
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
            _isCapturing = false;
            if (e.Exception != null)
            {
                Debug.WriteLine($"Recording stopped with error: {e.Exception.Message}");
            }
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