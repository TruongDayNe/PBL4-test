using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Networking;
using RealTimeUdpStream.Core.Audio;
using RealTimeUdpStream.Core.Input;
using RealTimeUdpStream.Core.Models;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WPFUI_NEW.Services;

namespace WPFUI_NEW.ViewModels
{
    public partial class HostViewModel : ObservableObject
    {
        private ScreenProcessor _screenProcessor;
        private ScreenSender _screenSender;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly NetworkService _networkService;

        private UdpPeer _sharedUdpPeer; // Peer chia sẻ
        private AudioManager _audioManager; // Quản lý audio
        private KeyboardManager _keyboardManager; // Quản lý keyboard

        [ObservableProperty] private BitmapSource previewImage = null!; // Initialize non-nullable fields
        [ObservableProperty] private string _streamButtonContent = "Bắt đầu Host";
        [ObservableProperty] private string _statusText = "Sẵn sàng";

        public IAsyncRelayCommand StartStreamCommand { get; }

        public HostViewModel()
        {
            _networkService = new NetworkService();
            _networkService.ClientConnected += OnClientConnected;
            StartStreamCommand = new AsyncRelayCommand(ToggleStreamingAsync);

            // Initialize non-nullable fields
            _screenProcessor = null!; // Mark as nullable or initialize properly
            _screenSender = null!; // Mark as nullable or initialize properly
            _cancellationTokenSource = null!; // Mark as nullable or initialize properly
            _sharedUdpPeer = null!; // Mark as nullable or initialize properly
            _audioManager = null!; // Mark as nullable or initialize properly
            _keyboardManager = null!; // Mark as nullable or initialize properly
        }

        private async Task ToggleStreamingAsync()
        {
            if (_screenSender != null)
            {
                // --- LOGIC DỪNG STREAM ---
                _cancellationTokenSource?.Cancel();
                _networkService.StopListening();
                Debug.WriteLine("[Host] Đã yêu cầu dừng stream/chờ...");

                _audioManager?.StopAudioStreaming();
                _audioManager?.Dispose();
                _audioManager = null;
                Debug.WriteLine("[Host] AudioManager dừng và hủy.");

                _keyboardManager?.StopSimulation();
                _keyboardManager?.Dispose();
                _keyboardManager = null;
                Debug.WriteLine("[Host] KeyboardManager dừng và hủy.");

                if (_screenSender != null)
                {
                    _screenSender.OnFrameCaptured -= HandleFrameCaptured;
                    _screenSender.Dispose();
                    _screenSender = null;
                }

                _screenProcessor?.Dispose();
                _screenProcessor = null;

                _sharedUdpPeer?.Dispose();
                _sharedUdpPeer = null;
                Debug.WriteLine("[Host] ScreenSender và ScreenProcessor dừng và hủy.");

                StreamButtonContent = "Bắt đầu Host";
                PreviewImage = null;
                StatusText = "Đã dừng. 0 client(s).";
                _cancellationTokenSource = null;
            }
            else
            {
                // --- LOGIC BẮT ĐẦU HOST ---
                try
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    const int SERVER_PORT = 12000;

                    _sharedUdpPeer = new UdpPeer(SERVER_PORT); // Tạo UdpPeer
                    
                    // BAT DAU LANG NGHE UDP - QUAN TRONG!
                    _ = Task.Run(() => _sharedUdpPeer.StartReceivingAsync(), _cancellationTokenSource.Token);
                    Console.WriteLine("[HOST] UdpPeer bat dau lang nghe tren port 12000");
                    Debug.WriteLine("[Host] UdpPeer StartReceivingAsync called.");

                    _screenProcessor = ScreenProcessor.Instance;
                    _screenProcessor.Start();
                    Debug.WriteLine("[Host] ScreenProcessor started.");

                    // Dùng UdpPeer chia sẻ cho ScreenSender
                    _screenSender = new ScreenSender(_sharedUdpPeer, _screenProcessor);
                    _screenSender.OnFrameCaptured += HandleFrameCaptured;
                    Debug.WriteLine("[Host] ScreenSender created.");

                    // HOST mode: không delay, phát ngay
                    _audioManager = new AudioManager(_sharedUdpPeer, AudioConfig.CreateDefault(), isClientMode: false);

                    _audioManager.StartAudioStreaming(AudioInputType.SystemAudio); // Bắt đầu ghi âm system
                    //_audioManager.StartAudioStreaming(AudioInputType.Microphone); // Bắt đầu ghi âm mic

                    Debug.WriteLine("[Host] AudioManager created and started.");

                    // HOST mode = TRUE = SIMULATE (nhận từ CLIENT và giả lập)
                    _keyboardManager = new KeyboardManager(_sharedUdpPeer, isClientMode: true);
                    _keyboardManager.StartSimulation(); // HOST NHẬN và GIẢ LẬP
                    Console.WriteLine("[HOST] Bat che do SIMULATION - se nhan va gia lap phim TFGH");
                    Debug.WriteLine("[Host] KeyboardManager SIMULATION started - se gia lap phim nhan tu CLIENT.");

                    // Bỏ await để không block UI thread
                    _ = Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    Debug.WriteLine("[Host] SendScreenLoopAsync started.");

                    _ = Task.Run(() => _networkService.StartTcpListenerLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Lỗi nghiêm trọng khi lắng nghe TCP (Port {NetworkService.HandshakePort} có đang được dùng?): {t.Exception?.InnerExceptions.FirstOrDefault()?.Message}", "Lỗi Mạng", MessageBoxButton.OK, MessageBoxImage.Error);
                                // Tự động dừng lại
                                ToggleStreamingAsync();
                            });
                        }
                    }, TaskScheduler.Default);
                    Debug.WriteLine("[Host] StartTcpListenerLoopAsync started.");

                    StatusText = "Đang stream... (0 client(s) connected)";
                    StreamButtonContent = "Dừng STREAM";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi bắt đầu Host: {ex.Message}");
                    _cancellationTokenSource?.Cancel();
                    _screenSender?.Dispose(); _screenSender = null;
                    _screenProcessor?.Dispose(); _screenProcessor = null;
                    StatusText = "Lỗi";
                    StreamButtonContent = "Bắt đầu Host";
                }
            }
        }

        private void OnClientConnected(string clientIp)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_debug.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] 🔗 OnClientConnected: ClientIP={clientIp}, _audioManager={(_audioManager == null ? "NULL" : "SET")}{Environment.NewLine}");
            
            if (_screenSender == null || _cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                Debug.WriteLine($"[Host] Client {clientIp} đã kết nối, nhưng Host không stream. Bỏ qua.");
                return;
            }

            try
            {
                const int CLIENT_PORT = 12001; // Đây là port Client lắng nghe
                var clientAddress = IPAddress.Parse(clientIp);
                var clientEndPoint = new IPEndPoint(clientAddress, CLIENT_PORT);

                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Setting target endpoint: {clientEndPoint}{Environment.NewLine}");

                // Gửi Video đến client
                _screenSender.AddClient(clientEndPoint);

                // Gửi Audio đến CÙNG client endpoint đó
                _audioManager.SetTargetEndPoint(clientEndPoint);
                
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] ✅ Target endpoint SET for audio!{Environment.NewLine}");

                Debug.WriteLine("[Host] ✅ Client connected - Keyboard simulation already running.");

                App.Current.Dispatcher.Invoke(() =>
                {
                    StatusText = $"Đang stream... ({_screenSender.ClientCount} client(s) connected)";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Lỗi khi thêm client {clientIp}: {ex.Message}");
            }
        }

        private void HandleFrameCaptured(Image frame)
        {
            App.Current.Dispatcher.Invoke(() => { PreviewImage = ToBitmapSource(frame); });
            frame.Dispose();
        }

        // --- Hàm chuyển đổi Image sang BitmapSource ---
        // SỬA LỖI: Khôi phục lại hàm gốc chính xác của bạn
        public static BitmapSource ToBitmapSource(Image image)
        {
            if (image == null) return null;
            Bitmap bitmap = null;
            System.Drawing.Imaging.BitmapData bitmapData = null;
            try
            {
                bitmap = new Bitmap(image);
                bitmapData = bitmap.LockBits(
                  new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                  System.Drawing.Imaging.ImageLockMode.ReadOnly,
                  System.Drawing.Imaging.PixelFormat.Format32bppRgb);

                var bitmapSource = BitmapSource.Create(
                  bitmapData.Width, bitmapData.Height,
                  bitmap.HorizontalResolution, bitmap.VerticalResolution,
                  System.Windows.Media.PixelFormats.Bgr32,
                  null,
                  bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
                bitmapSource.Freeze(); // Quan trọng cho đa luồng
                return bitmapSource;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi ToBitmapSource: {ex.Message}");
                return null;
            }
            finally
            {
                if (bitmapData != null)
                {
                    // SỬA LỖI: Dùng 'bitmap' thay vì '_bitmap' không tồn tại
                    bitmap?.UnlockBits(bitmapData);
                }
            }
        }

        public void Cleanup()
        {
            // Nếu đang stream, gọi Toggle để dừng và dọn dẹp
            if (_screenSender != null)
            {
                ToggleStreamingAsync().Wait(); // Chờ 
            }
        }
    }
}