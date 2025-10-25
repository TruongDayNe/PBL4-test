using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Networking;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WPFUI_NEW.Services;
using System.Linq; // Thêm using

namespace WPFUI_NEW.ViewModels
{
    public partial class HostViewModel : ObservableObject
    {
        private ScreenProcessor _screenProcessor;
        private ScreenSender _screenSender;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly NetworkService _networkService;

        [ObservableProperty] private BitmapSource _previewImage;
        [ObservableProperty] private string _streamButtonContent = "Bắt đầu Host";
        [ObservableProperty] private string _statusText = "Sẵn sàng";

        public IAsyncRelayCommand StartStreamCommand { get; }

        public HostViewModel()
        {
            _networkService = new NetworkService();
            _networkService.ClientConnected += OnClientConnected;
            StartStreamCommand = new AsyncRelayCommand(ToggleStreamingAsync);
        }

        private async Task ToggleStreamingAsync()
        {
            if (_screenSender != null)
            {
                // --- LOGIC DỪNG STREAM ---
                _cancellationTokenSource?.Cancel();
                _networkService.StopListening();
                Debug.WriteLine("[Host] Đã yêu cầu dừng stream/chờ...");

                if (_screenSender != null)
                {
                    _screenSender.OnFrameCaptured -= HandleFrameCaptured;
                    _screenSender.Dispose();
                    _screenSender = null;
                }

                _screenProcessor?.Dispose();
                _screenProcessor = null;

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

                    _screenProcessor = ScreenProcessor.Instance;
                    _screenProcessor.Start();
                    Debug.WriteLine("[Host] ScreenProcessor started.");

                    _screenSender = new ScreenSender(SERVER_PORT, _screenProcessor);
                    _screenSender.OnFrameCaptured += HandleFrameCaptured;
                    Debug.WriteLine("[Host] ScreenSender created.");

                    Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    Debug.WriteLine("[Host] SendScreenLoopAsync started.");

                    Task.Run(() => _networkService.StartTcpListenerLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token)
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
            if (_screenSender == null || _cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                Debug.WriteLine($"[Host] Client {clientIp} đã kết nối, nhưng Host không stream. Bỏ qua.");
                return;
            }

            try
            {
                const int CLIENT_PORT = 12001;
                var clientAddress = IPAddress.Parse(clientIp);
                var clientEndPoint = new IPEndPoint(clientAddress, CLIENT_PORT);

                _screenSender.AddClient(clientEndPoint);

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
    }
}