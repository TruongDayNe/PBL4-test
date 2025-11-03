using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Networking;
using RealTimeUdpStream.Core.Audio;
using RealTimeUdpStream.Core.Models; // Thêm using cho TelemetrySnapshot
using RealTimeUdpStream.Core.Networking; // Thêm using cho NetworkStats
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows; // Thêm using này cho MessageBox
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WPFUI_NEW.Services;

namespace WPFUI_NEW.ViewModels
{
    public partial class ClientViewModel : ObservableObject
    {
        private ScreenReceiver _screenReceiver;
        private readonly NetworkService _networkService;
        private DispatcherTimer _telemetryTimer;

        private UdpPeer _sharedUdpPeer; // Peer chia sẻ
        private AudioManager _audioManager;

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

                // Dọn dẹp AudioManager VÀ ScreenReceiver
                _audioManager?.StopAudioReceiving();
                _audioManager?.Dispose();
                _audioManager = null;

                _screenReceiver?.Stop(); // Stop cho chắc
                _screenReceiver?.Dispose(); // Dispose sẽ hủy đăng ký sự kiện
                _screenReceiver = null;

                // Dọn dẹp UdpPeer (SAU CÙNG)
                _sharedUdpPeer?.Dispose();
                _sharedUdpPeer = null;
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
                        // Tạo UdpPeer chung
                        _sharedUdpPeer = new UdpPeer(_clientPort); // _clientPort = 12001

                        // Dùng UdpPeer cho ScreenReceiver
                        _screenReceiver = new ScreenReceiver(_sharedUdpPeer);
                        _screenReceiver.OnFrameReady += HandleFrameReady;

                        _audioManager = new AudioManager(_sharedUdpPeer, AudioConfig.CreateDefault());
                        // AudioManager sẽ tự đăng ký OnPacketReceived với _sharedUdpPeer

                        _audioManager.StartAudioReceiving();

                        //_screenReceiver.Start(); 
                        //Không gọi _screenReceiver.Start() nữa vì AudioManager đã khởi động _sharedUdpPeer
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


        // --- ĐƯỢC GỌI MỖI GIÂY ---
        private void OnTelemetryTick(object sender, EventArgs e)
        {
            if (_sharedUdpPeer == null) return; // Kiểm tra _sharedUdpPeer     

            // Dùng thuộc tính 'Stats' từ UdpPeer chung
            var snapshot = _sharedUdpPeer.Stats.GetSnapshot();

            // Cập nhật các thuộc tính UI (giữ nguyên)
            PingText = $"{snapshot.Rtt.TotalMilliseconds:F0} ms";
            BitrateText = $"{snapshot.ReceivedBitrateKbps} Kbps";
            LossText = $"{snapshot.PacketLossRate:F1} %";
        }

        private void HandleFrameReady(BitmapSource frameSource)
        {
            // Logic y hệt HostViewModel:
            // Gửi ảnh nhận được từ luồng mạng về luồng UI
            // Dùng BeginInvoke để không khóa luồng nhận dữ liệu
            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ReceivedImage = frameSource;
            }));
        }
        public void Cleanup()
        {
            // Nếu đang kết nối, gọi Toggle để ngắt kết nối và dọn dẹp
            if (_screenReceiver != null)
            {
                ToggleConnectionAsync().Wait(); // Chờ
            }
        }
    }
}