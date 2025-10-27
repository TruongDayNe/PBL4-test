using NAudio.Wave;
using RealTimeUdpStream.Core.Models;
using System;
using System.Diagnostics;

namespace RealTimeUdpStream.Core.Audio
{
    /// <summary>
    /// Capture âm thanh từ microphone hoặc system audio
    /// </summary>
    public class AudioCapture : IDisposable
    {
        private readonly AudioConfig _config;
        private IWaveIn _waveIn;
        private WaveFormat _waveFormat;
        private bool _isCapturing = false;
        private bool _disposed = false;

        public event Action<AudioPacket> OnAudioDataAvailable;

        public AudioCapture(AudioConfig config, AudioInputType inputType = AudioInputType.Microphone)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeAudioInput(inputType);
        }

        private void InitializeAudioInput(AudioInputType inputType)
        {
            _waveFormat = new WaveFormat(_config.SampleRate, _config.BitsPerSample, _config.Channels);

            switch (inputType)
            {
                case AudioInputType.Microphone:
                    _waveIn = new WaveInEvent
                    {
                        WaveFormat = _waveFormat,
                        BufferMilliseconds = (int)_config.BufferDurationMs
                    };
                    break;

                case AudioInputType.SystemAudio:
                    _waveIn = new WasapiLoopbackCapture
                    {
                        WaveFormat = _waveFormat
                    };
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
                // Tạo AudioPacket từ dữ liệu nhận được
                var audioData = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, audioData, 0, e.BytesRecorded);

                var header = new AudioPacketHeader
                {
                    SequenceNumber = GenerateSequenceNumber(),
                    TimestampMs = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond),
                    Codec = _config.Codec,
                    SampleRate = _config.SampleRate,
                    Channels = _config.Channels,
                    SamplesPerChannel = e.BytesRecorded / (_config.Channels * (_config.BitsPerSample / 8)),
                    DataLength = (ushort)e.BytesRecorded
                };

                var packet = new AudioPacket(header, new ArraySegment<byte>(audioData));
                OnAudioDataAvailable?.Invoke(packet);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing audio data: {ex.Message}");
            }
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