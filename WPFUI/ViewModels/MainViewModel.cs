using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WPFUI.Services;

namespace WPFUI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ScreenProcessor _screenProcessor;
        private ScreenSender _screenSender;
        private ScreenReceiver _screenReceiver;
        private CancellationTokenSource _cancellationTokenSource;

        private const string CLIENT_IP = "127.0.0.1";
        private const int SERVER_PORT = 12000;
        private const int CLIENT_PORT = 12001;

        [ObservableProperty]
        private string _streamButtonContent = "Bắt đầu STREAM";

        [ObservableProperty]
        private string _receiveButtonContent = "Bắt đầu NHẬN";

        [ObservableProperty]
        private BitmapSource _previewImage;

        public MainViewModel()
        {
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

        [RelayCommand]
        private async Task StartStream()
        {
            if (_screenSender != null)
            {
                _cancellationTokenSource?.Cancel();
                if (_screenSender != null) _screenSender.OnFrameCaptured -= HandleFrameCaptured;
                _screenSender?.Dispose();
                _screenSender = null;
                StreamButtonContent = "Bắt đầu STREAM";
                PreviewImage = null;
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _screenSender = new ScreenSender(CLIENT_IP, CLIENT_PORT, SERVER_PORT, _screenProcessor);
            _screenSender.OnFrameCaptured += HandleFrameCaptured;
            StreamButtonContent = "Dừng STREAM";
            await Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token));
        }

        [RelayCommand]
        private void StartReceive()
        {
            if (_screenReceiver != null)
            {
                _screenReceiver.Stop();
                _screenReceiver.Dispose();
                _screenReceiver = null;
                ReceiveButtonContent = "Bắt đầu NHẬN";
                PreviewImage = null;
                return;
            }

            _screenReceiver = new ScreenReceiver(CLIENT_PORT);
            _screenReceiver.OnFrameReady += HandleFrameReady;
            _screenReceiver.Start();
            ReceiveButtonContent = "Dừng NHẬN";
        }

        [RelayCommand]
        private void TestOverlay()
        {
            var overlayWindow = new Views.OverlayWindow();
            overlayWindow.Show();
        }

        [RelayCommand]
        private void Cleanup()
        {
            _cancellationTokenSource?.Cancel();
            _screenSender?.Dispose();
            _screenReceiver?.Dispose();
            _screenProcessor?.Dispose();
        }

        private void HandleFrameCaptured(Image frame)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                PreviewImage = ToBitmapSource(frame);
            });
            frame.Dispose();
        }

        private void HandleFrameReady(BitmapSource frameSource)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                PreviewImage = frameSource;
            });
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