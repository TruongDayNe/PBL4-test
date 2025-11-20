using NAudio.Wave;
using RealTimeUdpStream.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RealTimeUdpStream.Core.Audio
{
    /// <summary>
    /// Ph�t �m thanh t? c�c AudioPacket nh?n du?c (v?i/kh�ng delay t�y ch? d?)
    /// </summary>
    public class AudioPlayback : IDisposable
    {
        private readonly AudioConfig _config;
        private readonly bool _enableDelay;
        private readonly int _delayDurationMs;
        private IWavePlayer _waveOut;
        private BufferedWaveProvider _waveProvider;
        private readonly ConcurrentQueue<AudioPacket> _audioQueue;
        private readonly ConcurrentQueue<DelayedAudioData> _delayQueue; // Queue cho delay
        private readonly Timer _bufferTimer;
        private readonly Timer _delayTimer; // Timer x? l� delay
        private readonly object _playbackLock = new object();
        private bool _disposed = false;
        private bool _isPlaying = false;
        
        // Opus decoder (nullable - only used if codec is OPUS)
        private OpusDecoder _opusDecoder;
        
        private static string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_debug.log");
        
        private void LogToFile(string message)
        {
            Debug.WriteLine($"[AudioPlayback] {message}");
        }

        // Buffer management - HIGH BUFFERING for maximum stability (trades latency for smoothness)
        private const int MAX_BUFFER_DURATION_MS = 1500; // 5 seconds maximum buffer
        private const int MIN_BUFFER_DURATION_MS = 500; // 5 seconds minimum before starting playback (high stability)

        // Delay data structure
        private class DelayedAudioData
        {
            public byte[] Data { get; set; }
            public long PlayAtTicks { get; set; }
        }

        public AudioPlayback(AudioConfig config, bool enableDelay = false, int delayMs = 3000)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _enableDelay = enableDelay;
            _delayDurationMs = delayMs;
            _audioQueue = new ConcurrentQueue<AudioPacket>();
            _delayQueue = new ConcurrentQueue<DelayedAudioData>();

            // Initialize Opus decoder if needed
            if (_config.Codec == AudioCodec.OPUS)
            {
                _opusDecoder = new OpusDecoder(
                    sampleRate: _config.SampleRate,
                    channels: _config.Channels
                );
                Debug.WriteLine($"✓ Opus decoder initialized: {_config.SampleRate}Hz, {_config.Channels}ch");
            }

            InitializePlayback();

            // Timer d? ki?m tra v� qu?n l� buffer (reduced frequency to reduce overhead)
            _bufferTimer = new Timer(ProcessAudioBuffer, null, 20, 20); // Check every 20ms (reduced from 10ms)
            
            // Timer d? x? l� delay buffer (ch? c?n n?u c� delay)
            if (_enableDelay)
            {
                _delayTimer = new Timer(ProcessDelayBuffer, null, 50, 50); // Check every 50ms
                Debug.WriteLine($"AudioPlayback initialized with {_delayDurationMs}ms delay");
                LogToFile($"Initialized with DELAY: {_delayDurationMs}ms");
            }
            else
            {
                Debug.WriteLine("AudioPlayback initialized WITHOUT delay (immediate playback)");
                LogToFile("Initialized WITHOUT delay (immediate playback)");
            }
        }

        private void InitializePlayback()
        {
            var waveFormat = new WaveFormat(_config.SampleRate, _config.BitsPerSample, _config.Channels);

            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(MAX_BUFFER_DURATION_MS),
                DiscardOnBufferOverflow = true, // Discard old data when buffer full to prevent memory bloat
                ReadFully = true // Read full buffers for smoother playback
            };

            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 800, // HIGH: 800ms latency for maximum smoothness
                NumberOfBuffers = 4   // Use 4 buffers for best stability
            };

            _waveOut.Init(_waveProvider);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
        }

        public void QueueAudioPacket(AudioPacket packet)
        {
            if (_disposed || packet == null) return;

            _audioQueue.Enqueue(packet);
            
            // Log d? debug
            if (_audioQueue.Count == 1 || _audioQueue.Count % 100 == 0)
            {
                Debug.WriteLine($"[AudioPlayback] Queued audio packet. Queue size: {_audioQueue.Count}, Delay mode: {_enableDelay}");
            }
        }

        private void ProcessAudioBuffer(object state)
        {
            if (_disposed) return;

            try
            {
                int queueCountBefore = _audioQueue.Count;
                int processedCount = 0;
                
                // X? l� t?t c? packets trong queue
                while (_audioQueue.TryDequeue(out AudioPacket packet))
                {
                    using (packet)
                    {
                        if (ValidateAudioPacket(packet))
                        {
                            AddToBuffer(packet);
                            processedCount++;
                        }
                        else
                        {
                            LogToFile($"!!! Packet FAILED validation: Expected {_config.SampleRate}Hz/{_config.Channels}ch, Got {packet.Header.SampleRate}Hz/{packet.Header.Channels}ch");
                        }
                    }
                }

                // Qu?n l� playback state
                ManagePlaybackState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing audio buffer: {ex.Message}");
                LogToFile($"ERROR in ProcessAudioBuffer: {ex.Message}");
            }
        }

        private bool ValidateAudioPacket(AudioPacket packet)
        {
            // Validate format matches expected config
            if (packet.Header.SampleRate != _config.SampleRate ||
                packet.Header.Channels != _config.Channels)
            {
                LogToFile($"!!! Format mismatch: Expected {_config.SampleRate}Hz/{_config.Channels}ch, " +
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
                        byte[] pcmData = null;
                        
                        // Decode Opus if needed
                        if (packet.Header.Codec == AudioCodec.OPUS && _opusDecoder != null)
                        {
                            try
                            {
                                // Extract Opus data from packet
                                byte[] opusData = new byte[audioData.Count];
                                Buffer.BlockCopy(audioData.Array, audioData.Offset, opusData, 0, audioData.Count);
                                
                                // Decode to PCM
                                pcmData = _opusDecoder.Decode(opusData);
                                // Debug.WriteLine($"🎵 Opus decoded: {opusData.Length} → {pcmData.Length} bytes");
                            }
                            catch (Exception decEx)
                            {
                                Debug.WriteLine($"❌ Opus decoding failed: {decEx.Message}");
                                return; // Skip this packet if decode fails
                            }
                        }
                        else
                        {
                            // PCM data - use directly
                            pcmData = new byte[audioData.Count];
                            Buffer.BlockCopy(audioData.Array, audioData.Offset, pcmData, 0, audioData.Count);
                        }
                        
                        if (pcmData == null || pcmData.Length == 0)
                            return;
                        
                        if (_enableDelay)
                        {
                            // CLIENT mode: Th�m v�o delay queue
                            var delayedItem = new DelayedAudioData
                            {
                                Data = pcmData,
                                PlayAtTicks = DateTime.UtcNow.Ticks + (_delayDurationMs * TimeSpan.TicksPerMillisecond)
                            };
                            
                            _delayQueue.Enqueue(delayedItem);
                            
                            // LOG d? debug - log thu?ng xuy�n hon
                            if (_delayQueue.Count == 1 || _delayQueue.Count % 50 == 0)
                            {
                                Debug.WriteLine($"[CLIENT DELAY MODE] Audio queued with {_delayDurationMs}ms delay. Queue size: {_delayQueue.Count}");
                                LogToFile($"Audio queued to delay buffer. Queue size: {_delayQueue.Count}");
                            }
                        }
                        else
                        {
                            // HOST mode: Ph�t ngay kh�ng delay
                            _waveProvider.AddSamples(pcmData, 0, pcmData.Length);
                            
                            if (DateTime.UtcNow.Ticks % 1000000 == 0) // Log th?nh tho?ng
                            {
                                Debug.WriteLine($"[HOST NO-DELAY MODE] Audio playing immediately. Buffer: {_waveProvider.BufferedDuration.TotalMilliseconds}ms");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error adding audio to buffer: {ex.Message}");
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
                        break;
                    }
                }

                if (processedCount > 0)
                {
                    Debug.WriteLine($"[CLIENT DELAY] PLAYING {processedCount} chunks AFTER {_delayDurationMs}ms delay! Queue remaining: {_delayQueue.Count}");
                    LogToFile($"PLAYING {processedCount} delayed chunks! Remaining in queue: {_delayQueue.Count}");
                    ManagePlaybackState(); // Ensure playback state is managed
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
                var bufferedBytes = _waveProvider.BufferedBytes;

                LogToFile($"ManagePlaybackState: BufferedMs={bufferedDurationMs:F0}, Bytes={bufferedBytes}, IsPlaying={_isPlaying}, MinRequired={MIN_BUFFER_DURATION_MS}");

                if (!_isPlaying && bufferedDurationMs >= MIN_BUFFER_DURATION_MS)
                {
                    // B?t d?u ph�t khi c� d? buffer
                    StartPlayback();
                }
                else if (_isPlaying && bufferedDurationMs <= 0)
                {
                    // D?ng ph�t khi h?t buffer
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
                LogToFile("*** PLAYBACK STARTED ***");
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
        public bool IsDelayEnabled => _enableDelay;
        public int DelayDurationMs => _delayDurationMs;

        public void Dispose()
        {
            if (_disposed) return;

            _bufferTimer?.Dispose();
            _delayTimer?.Dispose();
            StopPlayback();
            ClearBuffer();

            _waveOut?.Dispose();
            _waveProvider = null;
            _opusDecoder?.Dispose();

            _disposed = true;
            Debug.WriteLine("AudioPlayback disposed");
        }
    }
}
