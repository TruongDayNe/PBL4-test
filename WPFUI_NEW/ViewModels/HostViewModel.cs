using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFUI_NEW.Services;
using System.Diagnostics;

namespace WPFUI_NEW.ViewModels
{
    public partial class HostViewModel : ObservableObject
    {
        // THAY ĐỔI: Không khởi tạo _screenProcessor ở đây
        private ScreenProcessor _screenProcessor;
        private ScreenSender _screenSender;
        private CancellationTokenSource _cancellationTokenSource;

        [ObservableProperty]
        private BitmapSource _previewImage;

        [ObservableProperty]
        private string _streamButtonContent = "Bắt đầu STREAM";

        public IAsyncRelayCommand StartStreamCommand { get; }

        public HostViewModel()
        {
            // --- LOGIC MỚI ---
            // Hàm khởi tạo bây giờ "sạch", không gọi ScreenProcessor
            // Nó chỉ tạo Command.
            // Breakpoint (chấm đỏ) của bạn sẽ dừng ở đây.
            try
            {
                StartStreamCommand = new AsyncRelayCommand(ToggleStreamingAsync);
            }
            catch (Exception ex)
            {
                // Vẫn giữ lại để phòng trường hợp hiếm
                MessageBox.Show($"Lỗi không mong muốn khi tạo Command: {ex.Message}");
            }
        }

        private async Task ToggleStreamingAsync()
        {
            if (_screenSender != null)
            {
                // --- LOGIC DỪNG STREAM ---
                _cancellationTokenSource?.Cancel();
                Debug.WriteLine("Đã yêu cầu dừng stream...");

                _screenSender.OnFrameCaptured -= HandleFrameCaptured;
                _screenSender.Dispose();
                _screenSender = null;

                // Quan trọng: Hủy cả ScreenProcessor để nó có thể được tạo lại
                _screenProcessor?.Dispose();
                _screenProcessor = null;

                StreamButtonContent = "Bắt đầu STREAM";
                PreviewImage = null;
            }
            else
            {
                // --- LOGIC BẮT ĐẦU STREAM ---
                try
                {
                    // THAY ĐỔI: Khởi tạo ScreenProcessor CHỈ KHI BẤM NÚT
                    Debug.WriteLine("Đang khởi tạo ScreenProcessor...");
                    _screenProcessor = ScreenProcessor.Instance;
                    _screenProcessor.Start();
                    Debug.WriteLine("ScreenProcessor đã khởi động.");

                    _cancellationTokenSource = new CancellationTokenSource();
                    Debug.WriteLine("Đang bắt đầu stream...");

                    const string CLIENT_IP = "127.0.0.1";
                    const int SERVER_PORT = 12000;
                    const int CLIENT_PORT = 12001;

                    _screenSender = new ScreenSender(CLIENT_IP, CLIENT_PORT, SERVER_PORT, _screenProcessor);
                    _screenSender.OnFrameCaptured += HandleFrameCaptured;
                    StreamButtonContent = "Dừng STREAM";

                    Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token));
                }
                catch (Exception ex)
                {
                    // Chúng ta sẽ bắt được lỗi của SharpDX ở đây!
                    MessageBox.Show($"Lỗi khi bắt đầu stream (từ ScreenProcessor):\n{ex.Message}\n\nChi tiết:\n{ex.StackTrace}",
                                    "Lỗi Khởi Tạo Stream",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    StreamButtonContent = "Bắt đầu STREAM"; // Reset nút
                }
            }
        }

        private void HandleFrameCaptured(Image frame)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                PreviewImage = ToBitmapSource(frame);
            });
            frame.Dispose();
        }

        public static BitmapSource ToBitmapSource(Image image)
        {
            if (image == null) return null;
            using (var bitmap = new Bitmap(image))
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);
                try
                {
                    var bitmapSource = BitmapSource.Create(
                        bitmapData.Width, bitmapData.Height,
                        bitmap.HorizontalResolution, bitmap.VerticalResolution,
                        System.Windows.Media.PixelFormats.Bgr32, null,
                        bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
                    bitmapSource.Freeze();
                    return bitmapSource;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
        }
    }
}