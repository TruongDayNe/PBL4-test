using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using WPFUI_NEW.Services;
using System; // Cần cho Exception
using System.Diagnostics; // Cần cho Debug
using System.Windows.Threading;

namespace WPFUI_NEW.ViewModels
{
    public partial class ClientViewModel : ObservableObject
    {
        private ScreenReceiver _screenReceiver;
        private DispatcherTimer _telemetryTimer;

        // --- CÁC THUỘC TÍNH MỚI CHO OVERLAY ---
        [ObservableProperty]
        private string _pingText = "---";

        [ObservableProperty]
        private string _bitrateText = "---";

        [ObservableProperty]
        private string _lossText = "---";

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
            // Khởi tạo timer
            _telemetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // Tick 1 giây 1 lần
            };
            _telemetryTimer.Tick += OnTelemetryTick;
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
                _telemetryTimer.Stop(); 
                PingText = "---";
                BitrateText = "---";
                LossText = "---";
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
                    _telemetryTimer.Start(); // <-- Bắt đầu timer
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

        // --- HÀM MỚI: ĐƯỢC GỌI MỖI GIÂY ---
        private void OnTelemetryTick(object sender, EventArgs e)
        {
            if (_screenReceiver == null) return;

            // Dùng thuộc tính 'Stats' ta vừa tạo trong UdpPeer
            var snapshot = _screenReceiver.Peer.Stats.GetSnapshot();

            // Cập nhật các thuộc tính UI
            PingText = $"{snapshot.Rtt.TotalMilliseconds:F0} ms";
            BitrateText = $"{snapshot.ReceivedBitrateKbps} Kbps";
            LossText = $"{snapshot.PacketLossRate:F1} %";
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