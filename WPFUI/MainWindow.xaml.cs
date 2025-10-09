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
        private const string CLIENT_IP = "127.0.0.1";
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
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Chức năng này đã được tích hợp vào nút 'Bắt đầu STREAM'.", "Thông báo");
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
            _screenSender = new ScreenSender(CLIENT_IP, CLIENT_PORT, SERVER_PORT, _screenProcessor);
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