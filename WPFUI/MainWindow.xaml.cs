using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
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
        private string _serverIP = "127.0.0.1"; // IP của máy server (sender)
        private const int SERVER_PORT = 12000;
        private const int CLIENT_PORT = 12001;

        private readonly ScreenProcessor _screenProcessor;
        private ScreenSender _screenSender;
        private ScreenReceiver _screenReceiver;
        private CancellationTokenSource _cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            _screenProcessor = ScreenProcessor.Instance;
            try
            {
                _screenProcessor.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi khởi động ScreenProcessor: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            // Hiển thị IP hiện tại trên title bar
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            this.Title = $"Real-Time Screen Streaming - Server IP: {_serverIP}";
        }

        private void ConfigureServerIP()
        {
            string prompt = $"Nhập IP của máy server (sender):\n\n" +
                          $"• Để stream từ máy này: 127.0.0.1\n" +
                          $"• Để nhận từ máy khác: IP của máy đó\n" +
                          $"Ví dụ: 192.168.1.100\n\n" +
                          $"IP hiện tại: {_serverIP}";

            var result = MessageBox.Show(
                prompt + "\n\nBạn có muốn thay đổi IP không?",
                "Cấu hình Server IP",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Tạm thời sử dụng một prompt đơn giản
                var inputWindow = new Window
                {
                    Title = "Nhập Server IP",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                var stackPanel = new System.Windows.Controls.StackPanel
                {
                    Margin = new Thickness(20)
                };

                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = _serverIP,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var okButton = new System.Windows.Controls.Button
                {
                    Content = "OK",
                    Width = 100,
                    Height = 30,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                okButton.Click += (s, e) =>
                {
                    _serverIP = textBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(_serverIP))
                        _serverIP = "127.0.0.1";
                    inputWindow.DialogResult = true;
                    inputWindow.Close();
                };

                stackPanel.Children.Add(new System.Windows.Controls.Label { Content = "Server IP:" });
                stackPanel.Children.Add(textBox);
                stackPanel.Children.Add(okButton);
                inputWindow.Content = stackPanel;

                if (inputWindow.ShowDialog() == true)
                {
                    UpdateWindowTitle();
                }
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Sử dụng nút 'Cấu hình IP' để thay đổi địa chỉ server.", "Thông báo");
        }

        private void ConfigIPBtn_Click(object sender, RoutedEventArgs e)
        {
            ConfigureServerIP();
        }

        private void StartStreamBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_screenSender != null)
            {
                _cancellationTokenSource?.Cancel();
                _screenSender.OnFrameCaptured -= HandleFrameCaptured;
                _screenSender.Dispose();
                _screenSender = null;
                startStreamBtn.Content = "Bắt đầu STREAM";
                pnScreen.Source = null;
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _screenSender = new ScreenSender(_serverIP, CLIENT_PORT, SERVER_PORT, _screenProcessor);
            _screenSender.OnFrameCaptured += HandleFrameCaptured;
            startStreamBtn.Content = "Dừng STREAM";
            Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token));
        }

        private void HandleFrameCaptured(Image frame)
        {
            Dispatcher.Invoke(() =>
            {
                this.pnScreen.Source = ToBitmapSource(frame);
            });
            frame.Dispose();
        }

        private void StartReceiveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_screenReceiver != null)
            {
                _screenReceiver.Stop();
                _screenReceiver.Dispose();
                _screenReceiver = null;
                startReceiveBtn.Content = "Bắt đầu NHẬN";
                pnScreen.Source = null;
                return;
            }

            _screenReceiver = new ScreenReceiver(CLIENT_PORT);
            _screenReceiver.OnFrameReady += HandleFrameReady;
            _screenReceiver.Start();
            startReceiveBtn.Content = "Dừng NHẬN";
        }

        private void TestOverlayBtn_Click(object sender, RoutedEventArgs e)
        {
            // Tạo một thể hiện của cửa sổ overlay mới và hiển thị nó
            var overlayWindow = new OverlayWindow();
            overlayWindow.Show();
        }

        private void HandleFrameReady(BitmapSource frameSource)
        {
            Dispatcher.Invoke(() =>
            {
                this.pnScreen.Source = frameSource;
            });
        }

        public static BitmapSource ToBitmapSource(Image image)
        {
            if (image == null) return null;
            using (var bitmap = new Bitmap(image))
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);
                try
                {
                    var bitmapSource = BitmapSource.Create(
                        bitmapData.Width, bitmapData.Height,
                        bitmap.HorizontalResolution, bitmap.VerticalResolution,
                        PixelFormats.Bgr32, null,
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

        protected override void OnClosing(CancelEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _screenSender?.Dispose();
            _screenReceiver?.Dispose();
            _screenProcessor?.Dispose();
            base.OnClosing(e);
        }
    }
}