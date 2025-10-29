using NAudio.Wave;
using RealTimeUdpStream.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RealTimeUdpStream.Core.Audio
{
    /// <summary>
    /// Phát âm thanh từ các AudioPacket nhận được với độ trễ 5 giây
    /// </summary>
    public class AudioPlayback : IDisposable
    {
        private readonly AudioConfig _config;
        private IWavePlayer _waveOut;
        private BufferedWaveProvider _waveProvider;
        private readonly ConcurrentQueue<AudioPacket> _audioQueue;
        private readonly ConcurrentQueue<DelayedAudioData> _delayQueue; // Queue cho delay
        private readonly Timer _bufferTimer;
        private readonly Timer _delayTimer; // Timer xử lý delay
        private readonly object _playbackLock = new object();
        private bool _disposed = false;
        private bool _isPlaying = false;

        // Buffer management
        private const int MAX_BUFFER_DURATION_MS = 1000; // 1 second maximum buffer
        private const int MIN_BUFFER_DURATION_MS = 100;  // 100ms minimum before starting playback
        private const int DELAY_DURATION_MS = 5000; // 5 giây độ trễ

        // Delay data structure
        private class DelayedAudioData
        {
            public byte[] Data { get; set; }
            public long PlayAtTicks { get; set; }
        }

        public AudioPlayback(AudioConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _audioQueue = new ConcurrentQueue<AudioPacket>();
            _delayQueue = new ConcurrentQueue<DelayedAudioData>();

            InitializePlayback();

            // Timer để kiểm tra và quản lý buffer
            _bufferTimer = new Timer(ProcessAudioBuffer, null, 10, 10); // Check every 10ms
            
            // Timer để xử lý delay buffer
            _delayTimer = new Timer(ProcessDelayBuffer, null, 50, 50); // Check every 50ms
            
            Debug.WriteLine($"AudioPlayback initialized with {DELAY_DURATION_MS}ms delay");
        }

        private void InitializePlayback()
        {
            var waveFormat = new WaveFormat(_config.SampleRate, _config.BitsPerSample, _config.Channels);

            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(MAX_BUFFER_DURATION_MS),
                DiscardOnBufferOverflow = true // Bỏ qua data cũ nếu buffer đầy
            };

            _waveOut = new WaveOutEvent
            {
                DesiredLatency = (int)_config.BufferDurationMs
            };

            _waveOut.Init(_waveProvider);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
        }

        public void QueueAudioPacket(AudioPacket packet)
        {
            if (_disposed || packet == null) return;

            _audioQueue.Enqueue(packet);
        }

        private void ProcessAudioBuffer(object state)
        {
            if (_disposed) return;

            try
            {
                // Xử lý tất cả packets trong queue
                while (_audioQueue.TryDequeue(out AudioPacket packet))
                {
                    using (packet)
                    {
                        if (ValidateAudioPacket(packet))
                        {
                            AddToBuffer(packet);
                        }
                    }
                }

                // Quản lý playback state
                ManagePlaybackState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing audio buffer: {ex.Message}");
            }
        }

        private bool ValidateAudioPacket(AudioPacket packet)
        {
            // Kiểm tra tính hợp lệ của packet
            if (packet.Header.SampleRate != _config.SampleRate ||
                packet.Header.Channels != _config.Channels)
            {
                Debug.WriteLine($"Audio format mismatch: Expected {_config.SampleRate}Hz/{_config.Channels}ch, " +
                               $"Got {packet.Header.SampleRate}Hz/{packet.Header.Channels}ch");
                return false;
            }

            return true;
        }

        private void AddToBuffer(AudioPacket packet)
        {
            lock (_playbackLock)
            {
                try
                {
                    var audioData = packet.AudioData;
                    if (audioData.Count > 0)
                    {
                        // Thêm vào delay queue thay vì phát ngay
                        byte[] delayedData = new byte[audioData.Count];
                        Buffer.BlockCopy(audioData.Array, audioData.Offset, delayedData, 0, audioData.Count);
                        
                        var delayedItem = new DelayedAudioData
                        {
                            Data = delayedData,
                            PlayAtTicks = DateTime.UtcNow.Ticks + (DELAY_DURATION_MS * TimeSpan.TicksPerMillisecond)
                        };
                        
                        _delayQueue.Enqueue(delayedItem);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error adding audio to delay buffer: {ex.Message}");
                }
            }
        }

        private void ProcessDelayBuffer(object state)
        {
            if (_disposed) return;

            try
            {
                long currentTicks = DateTime.UtcNow.Ticks;
                int processedCount = 0;

                // Xử lý tất cả audio data đã đến thời gian phát
                while (_delayQueue.TryPeek(out DelayedAudioData delayedItem))
                {
                    if (delayedItem.PlayAtTicks <= currentTicks)
                    {
                        if (_delayQueue.TryDequeue(out delayedItem))
                        {
                            lock (_playbackLock)
                            {
                                _waveProvider.AddSamples(delayedItem.Data, 0, delayedItem.Data.Length);
                                processedCount++;
                            }
                        }
                    }
                    else
                    {
                        break; // Chưa đến thời gian phát
                    }
                }

                if (processedCount > 0)
                {
                    Debug.WriteLine($"Processed {processedCount} delayed audio chunks. Delay queue: {_delayQueue.Count}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing delay buffer: {ex.Message}");
            }
        }

        private void ManagePlaybackState()
        {
            lock (_playbackLock)
            {
                var bufferedDurationMs = _waveProvider.BufferedDuration.TotalMilliseconds;

                if (!_isPlaying && bufferedDurationMs >= MIN_BUFFER_DURATION_MS)
                {
                    // Bắt đầu phát khi có đủ buffer
                    StartPlayback();
                }
                else if (_isPlaying && bufferedDurationMs <= 0)
                {
                    // Dừng phát khi hết buffer
                    StopPlayback();
                }
            }
        }

        private void StartPlayback()
        {
            if (_disposed || _isPlaying) return;

            try
            {
                _waveOut.Play();
                _isPlaying = true;
                Debug.WriteLine("Audio playback started");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start audio playback: {ex.Message}");
            }
        }

        private void StopPlayback()
        {
            if (!_isPlaying) return;

            try
            {
                _waveOut.Stop();
                _isPlaying = false;
                Debug.WriteLine("Audio playback stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping audio playback: {ex.Message}");
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            _isPlaying = false;
            if (e.Exception != null)
            {
                Debug.WriteLine($"Playback stopped with error: {e.Exception.Message}");
            }
        }

        public void ClearBuffer()
        {
            lock (_playbackLock)
            {
                _waveProvider?.ClearBuffer();
                while (_audioQueue.TryDequeue(out AudioPacket packet))
                {
                    packet?.Dispose();
                }
                while (_delayQueue.TryDequeue(out DelayedAudioData delayedData))
                {
                    // Clear delay queue
                }
            }
        }

        public double BufferedDurationMs => _waveProvider?.BufferedDuration.TotalMilliseconds ?? 0;
        public int DelayQueueCount => _delayQueue?.Count ?? 0;
        public bool IsPlaying => _isPlaying;

        public void Dispose()
        {
            if (_disposed) return;

            _bufferTimer?.Dispose();
            _delayTimer?.Dispose();
            StopPlayback();
            ClearBuffer();

            _waveOut?.Dispose();
            _waveProvider = null;

            _disposed = true;
            Debug.WriteLine("AudioPlayback disposed");
        }
    }
}