using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealTimeUdpStream.Core.Models; // Thêm using cho TelemetrySnapshot
using RealTimeUdpStream.Core.Networking; // Thêm using cho NetworkStats
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows; // Thêm using này cho MessageBox
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WPFUI_NEW.Services;
using Core.Networking;

namespace WPFUI_NEW.ViewModels
{
    public partial class ClientViewModel : ObservableObject
    {
        private ScreenReceiver _screenReceiver;
        private readonly NetworkService _networkService;
        private DispatcherTimer _telemetryTimer;

        // --- Thuộc tính cho Telemetry ---
        [ObservableProperty] private string _pingText = "---";
        [ObservableProperty] private string _bitrateText = "---";
        [ObservableProperty] private string _lossText = "---";

        // --- Thuộc tính cho UI ---
        [ObservableProperty] private BitmapSource _receivedImage;
        [ObservableProperty] private string _connectButtonContent = "Kết nối";
        [ObservableProperty] private string _hostIpAddress = "127.0.0.1"; // IP Host cần nhập
        [ObservableProperty] private int _clientPort = 12001; // Port Client lắng nghe UDP

        public IAsyncRelayCommand ConnectCommand { get; }

        public ClientViewModel()
        {
            _networkService = new NetworkService();
            ConnectCommand = new AsyncRelayCommand(ToggleConnectionAsync);

            // Khởi tạo timer telemetry
            _telemetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _telemetryTimer.Tick += OnTelemetryTick;
        }

        private async Task ToggleConnectionAsync()
        {
            if (_screenReceiver != null)
            {
                // --- LOGIC NGẮT KẾT NỐI ---
                Debug.WriteLine("[Client] Đang ngắt kết nối UDP...");
                _telemetryTimer.Stop(); // Dừng timer trước
                _screenReceiver.Stop();
                _screenReceiver.OnFrameReady -= HandleFrameReady;
                _screenReceiver.Dispose();
                _screenReceiver = null;
                ConnectButtonContent = "Kết nối";
                ReceivedImage = null; // Xóa ảnh
                // Reset telemetry text
                PingText = "---";
                BitrateText = "---";
                LossText = "---";
            }
            else
            {
                // --- LOGIC KẾT NỐI MỚI ---
                Debug.WriteLine($"[Client] Bắt đầu quy trình kết nối tới Host: {HostIpAddress}");
                ConnectButtonContent = "Đang kết nối...";

                // 1. Kết nối TCP để gửi IP
                bool tcpSuccess = await _networkService.ConnectToHostAsync(HostIpAddress);

                if (tcpSuccess)
                {
                    Debug.WriteLine($"[Client] Gửi IP thành công. Bắt đầu lắng nghe UDP trên port {_clientPort}...");
                    // 2. Nếu TCP thành công, bắt đầu ScreenReceiver (UDP)
                    try
                    {
                        _screenReceiver = new ScreenReceiver(_clientPort);
                        _screenReceiver.OnFrameReady += HandleFrameReady;
                        _screenReceiver.Start();
                        ConnectButtonContent = "Ngắt kết nối";
                        _telemetryTimer.Start(); // Bắt đầu timer telemetry
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi bắt đầu nhận UDP (Port {_clientPort} có thể đang được sử dụng?): {ex.Message}", "Lỗi UDP", MessageBoxButton.OK, MessageBoxImage.Error);
                        ConnectButtonContent = "Kết nối";
                    }
                }
                else
                {
                    MessageBox.Show($"Không thể kết nối TCP đến Host tại {HostIpAddress}:{NetworkService.HandshakePort}. Hãy đảm bảo Host đang chạy và kiểm tra tường lửa.", "Lỗi Kết Nối TCP", MessageBoxButton.OK, MessageBoxImage.Error);
                    ConnectButtonContent = "Kết nối";
                }
            }
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