using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPFUI.Graphics;

namespace WPFUI
{
    public partial class MainWindow : Window
    {
        // Các cổng và địa chỉ cho DEMO
        private const string CLIENT_IP = "127.0.0.1";
        private const int SERVER_PORT = 12000; // Cổng gửi/nghe của Sender
        private const int CLIENT_PORT = 12001; // Cổng gửi/nghe của Receiver

        public ScreenProcessor _screenProcessor = null;
        private ScreenSender _screenSender = null;
        private ScreenReceiver _screenReceiver = null;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning = false;

        public MainWindow()
        {
            InitializeComponent();
            this._screenProcessor = ScreenProcessor.Instance;
            try
            {
                this._screenProcessor.Start();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Lỗi khi khởi động ScreenProcessor: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _isRunning = true;
            startBtn.Content = "Stop";

            // Sử dụng delegate pattern
            Action action = delegate ()
            {
                while (_isRunning)
                {
                    try
                    {
                        using (System.Drawing.Image img = _screenProcessor.CurrentScreenImage)
                        {
                            if (img != null)
                            {
                                // Convert sang BitmapSource
                                BitmapSource bitmapSource = ToBitmapSource(img);

                                // Cập nhật UI - PHẢI dùng Dispatcher trong WPF
                                this.Dispatcher.Invoke(() =>
                                {
                                    this.pnScreen.Source = bitmapSource;
                                });
                            }
                        }

                        // 30 FPS
                        Thread.Sleep(33);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error: {ex.Message}");
                        _isRunning = false;
                    }
                }
            };

            Task.Run(action);
        }

        // Trong MainWindow.xaml.cs
        private void StartStreamBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_screenSender != null)
            {
                // Logic dừng nếu đang chạy
                _cancellationTokenSource?.Cancel();
                _screenSender.Dispose();
                _screenSender = null;
                startStreamBtn.Content = "Bắt đầu STREAM";
                return;
            }

            // Khởi tạo Sender (Server) và luồng token
            _cancellationTokenSource = new CancellationTokenSource();
            _screenSender = new ScreenSender(CLIENT_IP, CLIENT_PORT, SERVER_PORT, _screenProcessor);
            startStreamBtn.Content = "Dừng STREAM";

            // Bắt đầu vòng lặp gửi trên luồng nền
            Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token));
        }

        private void StartReceiveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_screenReceiver != null)
            {
                // Logic dừng
                _screenReceiver.Stop();
                _screenReceiver.Dispose();
                _screenReceiver = null;
                startReceiveBtn.Content = "Bắt đầu NHẬN";
                pnScreen.Source = null; // Xóa màn hình
                return;
            }

            // Khởi tạo Receiver (Client)
            _screenReceiver = new ScreenReceiver(CLIENT_PORT);
            _screenReceiver.OnFrameReady += HandleFrameReady;

            _screenReceiver.Start();
            startReceiveBtn.Content = "Dừng NHẬN";
        }

        // Phương thức xử lý khung hình nhận được
        private void HandleFrameReady(BitmapSource frameSource)
        {
            // PHẢI dùng Dispatcher để cập nhật UI từ luồng nền của Receiver
            this.Dispatcher.Invoke(() =>
            {
                this.pnScreen.Source = frameSource;
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        // test với 1 frame
        private async void TestSendFrameBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_screenSender == null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _screenSender = new ScreenSender(CLIENT_IP, CLIENT_PORT, SERVER_PORT, _screenProcessor);
                MessageBox.Show("Đã tự động khởi tạo Sender để test. Hãy đảm bảo Receiver cũng đang chạy.", "Thông báo");
            }

            try
            {
                // Gọi phương thức và nhận lại ảnh đã chụp
                System.Drawing.Image capturedImage = await _screenSender.SendSingleFrameAsync();

                // Kiểm tra xem ảnh có được trả về thành công không
                if (capturedImage != null)
                {
                    // Sử dụng phương thức ToBitmapSource có sẵn để chuyển đổi và hiển thị
                    this.pnScreen.Source = ToBitmapSource(capturedImage);

                    // Giải phóng bộ nhớ cho đối tượng ảnh sau khi đã dùng xong
                    capturedImage.Dispose();

                    MessageBox.Show("Đã gửi và hiển thị frame test!", "Hoàn tất");
                }
                else
                {
                    MessageBox.Show("Không thể chụp hoặc gửi frame. Hãy kiểm tra cửa sổ Output.", "Thất bại", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi gửi frame test: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Chuyển ảnh System.Drawing.Image sang BitmapSource
        public static BitmapSource ToBitmapSource(System.Drawing.Image image)
        {
            if (image == null)
                return null;

            try
            {
                Bitmap bitmap = new Bitmap(image);

                var bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);

                try
                {
                    var bitmapSource = BitmapSource.Create(
                        bitmapData.Width,
                        bitmapData.Height,
                        bitmap.HorizontalResolution,
                        bitmap.VerticalResolution,
                        PixelFormats.Bgr32,
                        null,
                        bitmapData.Scan0,
                        bitmapData.Stride * bitmapData.Height,
                        bitmapData.Stride);

                    bitmapSource.Freeze(); // Quan trọng cho multi-threading
                    return bitmapSource;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ToBitmapSource Error: {ex.Message}");
                return null;
            }
        }

        // Trong MainWindow.xaml.cs
        protected override void OnClosing(CancelEventArgs e)
        {
            // Dừng stream nếu đang chạy
            _cancellationTokenSource?.Cancel();
            _screenSender?.Dispose();

            // Dừng nhận nếu đang chạy
            _screenReceiver?.Stop();
            _screenReceiver?.Dispose();

            this._screenProcessor?.Dispose();
            base.OnClosing(e);
        }
    }

}
