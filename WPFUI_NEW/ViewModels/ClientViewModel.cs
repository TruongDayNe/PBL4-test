using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using WPFUI_NEW.Services;
using System; // Cần cho Exception
using System.Diagnostics; // Cần cho Debug

namespace WPFUI_NEW.ViewModels
{
    // Đảm bảo là 'public partial' và kế thừa 'ObservableObject'
    public partial class ClientViewModel : ObservableObject
    {
        private ScreenReceiver _screenReceiver;

        // --- Thuộc tính (Properties) cho UI Binding ---

        [ObservableProperty]
        private BitmapSource _receivedImage;

        [ObservableProperty]
        private string _connectButtonContent = "Kết nối";

        // Thuộc tính để bind với TextBox nhập IP
        // IP của Host KHÔNG dùng ở đây, mà dùng ở file ScreenSender của Host.
        // Client chỉ cần biết Port MÌNH sẽ lắng nghe.
        [ObservableProperty]
        private string _hostIpAddress = "127.0.0.1"; // Tạm thời để đây, ta sẽ dùng sau

        [ObservableProperty]
        private int _clientPort = 12001; // Đây là port MÁY NÀY lắng nghe

        // --- Command ---
        public IAsyncRelayCommand ConnectCommand { get; }

        public ClientViewModel()
        {
            ConnectCommand = new AsyncRelayCommand(ToggleConnectionAsync);
        }

        private async Task ToggleConnectionAsync()
        {
            if (_screenReceiver != null)
            {
                // --- LOGIC NGẮT KẾT NỐI ---
                Debug.WriteLine("Đang ngắt kết nối...");
                _screenReceiver.Stop();
                _screenReceiver.OnFrameReady -= HandleFrameReady;
                _screenReceiver.Dispose();
                _screenReceiver = null;
                ConnectButtonContent = "Kết nối";
                ReceivedImage = null;
            }
            else
            {
                // --- LOGIC KẾT NỐI ---
                Debug.WriteLine($"Đang kết nối tới port: {_clientPort}...");
                try
                {
                    // Lấy port để lắng nghe từ UI
                    _screenReceiver = new ScreenReceiver(_clientPort);

                    // Đăng ký sự kiện
                    _screenReceiver.OnFrameReady += HandleFrameReady;

                    // Bắt đầu chạy vòng lặp nhận trên luồng nền
                    _screenReceiver.Start();

                    ConnectButtonContent = "Ngắt kết nối";
                }
                catch (Exception ex)
                {
                    // Thường lỗi do Port đã được sử dụng
                    System.Windows.MessageBox.Show($"Lỗi khi kết nối (Port {_clientPort} có thể đang được Host sử dụng?): {ex.Message}");
                    ConnectButtonContent = "Kết nối";
                }
            }
            // Không cần await gì cả
            await Task.CompletedTask;
        }

        private void HandleFrameReady(BitmapSource frameSource)
        {
            // Logic y hệt HostViewModel:
            // Gửi ảnh nhận được từ luồng mạng về luồng UI
            App.Current.Dispatcher.Invoke(() =>
            {
                ReceivedImage = frameSource;
            });
        }
    }
}