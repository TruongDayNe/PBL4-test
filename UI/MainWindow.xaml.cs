using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UI
{
    public partial class MainWindow : Window
    {
        public ScreenProcessor _screenProcessor = null;
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

        protected override void OnClosing(CancelEventArgs e)
        {
            this._screenProcessor?.Dispose();
            base.OnClosing(e);
        }
    }

}
