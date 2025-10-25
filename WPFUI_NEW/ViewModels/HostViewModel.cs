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
using Core.Networking;

namespace WPFUI_NEW.ViewModels
{
    public partial class HostViewModel : ObservableObject
    {
        private ScreenProcessor _screenProcessor;
        private ScreenSender _screenSender;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly NetworkService _networkService;
        private Task _listenTcpTask; // Thêm Task để quản lý việc lắng nghe

        [ObservableProperty] private BitmapSource _previewImage;
        [ObservableProperty] private string _streamButtonContent = "Bắt đầu Host";
        [ObservableProperty] private string _statusText = "Sẵn sàng";

        private string _connectedClientIp = null;

        public IAsyncRelayCommand StartStreamCommand { get; }

        public HostViewModel()
        {
            _networkService = new NetworkService();
            _networkService.ClientConnected += OnClientConnected;
            StartStreamCommand = new AsyncRelayCommand(ToggleStreamingAsync);
        }

        private async Task ToggleStreamingAsync()
        {
            if (_screenSender != null || _statusText == "Đang chờ Client...")
            {
                // --- LOGIC DỪNG HOST HOẶC HỦY CHỜ ---
                _cancellationTokenSource?.Cancel(); // Yêu cầu dừng các Task đang chạy (UDP stream và TCP listen)
                Debug.WriteLine("[Host] Đã yêu cầu dừng stream/chờ...");

                // Dừng ScreenSender nếu đang chạy
                if (_screenSender != null)
                {
                    _screenSender.OnFrameCaptured -= HandleFrameCaptured;
                    _screenSender.Dispose();
                    _screenSender = null;
                }

                // Dừng ScreenProcessor nếu đang chạy
                _screenProcessor?.Dispose();
                _screenProcessor = null;

                // Đợi Task lắng nghe TCP kết thúc (nếu đang chạy)
                if (_listenTcpTask != null && !_listenTcpTask.IsCompleted)
                {
                    try { await _listenTcpTask; } // Chờ nó hoàn thành hoặc bị hủy
                    catch (OperationCanceledException) { } // Bỏ qua lỗi hủy
                    catch (Exception ex) { Debug.WriteLine($"[Host] Lỗi khi chờ TCP listen task: {ex.Message}"); }
                    _listenTcpTask = null;
                }

                StreamButtonContent = "Bắt đầu Host";
                PreviewImage = null;
                StatusText = "Đã dừng";
                _connectedClientIp = null;
                _cancellationTokenSource = null; // Reset CancellationTokenSource
            }
            else
            {
                // --- LOGIC BẮT ĐẦU HOST ---
                try
                {
                    StatusText = "Đang chờ Client...";
                    StreamButtonContent = "Hủy Chờ";
                    _connectedClientIp = null; // Reset IP client cũ

                    _cancellationTokenSource = new CancellationTokenSource(); // Tạo token mới cho lần chạy này

                    // Bắt đầu lắng nghe TCP trên luồng nền và lưu lại Task
                    _listenTcpTask = Task.Run(() => _networkService.StartListeningForClientAsync(), _cancellationTokenSource.Token);

                    // Xử lý trường hợp Task lắng nghe bị lỗi hoặc bị hủy
                    _listenTcpTask.ContinueWith(t =>
                    {
                        if (t.IsFaulted || t.IsCanceled)
                        {
                            // Nếu lỗi xảy ra TRƯỚC KHI client kết nối, reset UI về trạng thái ban đầu
                            if (_connectedClientIp == null)
                            {
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    if (!_cancellationTokenSource.IsCancellationRequested) // Chỉ báo lỗi nếu không phải do người dùng hủy
                                    {
                                        MessageBox.Show($"Lỗi khi chờ Client kết nối (Port {NetworkService.HandshakePort} có đang được dùng?): {t.Exception?.InnerExceptions.FirstOrDefault()?.Message ?? "Task cancelled"}");
                                        StatusText = "Lỗi";
                                    }
                                    else
                                    {
                                        StatusText = "Đã hủy chờ";
                                    }
                                    StreamButtonContent = "Bắt đầu Host";
                                });
                            }
                        }
                    }, TaskScheduler.Default); // Chạy trên luồng bất kỳ
                }
                catch (Exception ex) // Lỗi đồng bộ khi khởi tạo Task.Run (hiếm)
                {
                    MessageBox.Show($"Lỗi khi bắt đầu lắng nghe Client: {ex.Message}");
                    StatusText = "Lỗi";
                    StreamButtonContent = "Bắt đầu Host";
                }
            }
        }

        private void OnClientConnected(string clientIp)
        {
            // Đảm bảo chỉ bắt đầu stream nếu chúng ta đang ở trạng thái chờ
            if (StatusText != "Đang chờ Client...") return;

            App.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _connectedClientIp = clientIp;
                    StatusText = $"Client {clientIp} đã kết nối. Bắt đầu stream...";

                    // Khởi tạo ScreenProcessor
                    _screenProcessor = ScreenProcessor.Instance;
                    _screenProcessor.Start();

                    // Khởi tạo ScreenSender
                    const int SERVER_PORT = 12000;
                    const int CLIENT_PORT = 12001;
                    _screenSender = new ScreenSender(_connectedClientIp, CLIENT_PORT, SERVER_PORT, _screenProcessor);
                    _screenSender.OnFrameCaptured += HandleFrameCaptured;
                    StreamButtonContent = "Dừng STREAM";

                    // Bắt đầu vòng lặp gửi UDP (sử dụng CancellationTokenSource hiện có)
                    Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi bắt đầu stream sau khi Client kết nối:\n{ex.Message}", "Lỗi Stream", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Reset về trạng thái ban đầu nếu lỗi
                    _cancellationTokenSource?.Cancel();
                    _screenSender?.Dispose(); _screenSender = null;
                    _screenProcessor?.Dispose(); _screenProcessor = null;
                    StatusText = "Lỗi";
                    StreamButtonContent = "Bắt đầu Host";
                    _connectedClientIp = null;
                }
            });
        }

        private void HandleFrameCaptured(Image frame)
        {
            // Gửi ảnh về luồng UI để hiển thị
            App.Current.Dispatcher.Invoke(() => { PreviewImage = ToBitmapSource(frame); });
            // Dispose ảnh gốc sau khi đã dùng xong (ToBitmapSource tạo bản sao)
            frame.Dispose();
        }

        // --- Hàm chuyển đổi Image sang BitmapSource ---
        // (Giữ nguyên hàm ToBitmapSource bạn đã có)
        public static BitmapSource ToBitmapSource(Image image)
        {
            if (image == null) return null;
            // Dùng try-finally để đảm bảo UnlockBits luôn được gọi
            Bitmap bitmap = null;
            System.Drawing.Imaging.BitmapData bitmapData = null;
            try
            {
                bitmap = new Bitmap(image);
                bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    // Sử dụng định dạng Bgr32 vì nó tương thích trực tiếp với WPF
                    System.Drawing.Imaging.PixelFormat.Format32bppRgb); // Hoặc Format32bppArgb nếu ảnh gốc có alpha

                var bitmapSource = BitmapSource.Create(
                    bitmapData.Width, bitmapData.Height,
                    bitmap.HorizontalResolution, bitmap.VerticalResolution,
                    System.Windows.Media.PixelFormats.Bgr32, // Đảm bảo khớp với PixelFormat ở trên
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
                    bitmap?.UnlockBits(bitmapData);
                }
                // Không dispose bitmap ở đây vì nó được tạo từ image đầu vào
            }
        }
    }
}

