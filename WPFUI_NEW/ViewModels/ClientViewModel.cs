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
        [ObservableProperty] private int clientPort = 12001; // Replace _clientPort with generated property

        public IAsyncRelayCommand ConnectCommand { get; }

        public ClientViewModel()
        {
            _networkService = new NetworkService();
            ConnectCommand = new AsyncRelayCommand(ToggleConnectionAsync);

            // Initialize non-nullable fields
            _screenReceiver = null!; // Mark as nullable or initialize properly
            _sharedUdpPeer = null!; // Mark as nullable or initialize properly
            _audioManager = null!; // Mark as nullable or initialize properly
            _receivedImage = null!; // Mark as nullable or initialize properly

            // Initialize telemetry timer
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
                // Disconnect logic
                _telemetryTimer.Stop();
                _audioManager?.StopAudioReceiving();
                _audioManager?.Dispose();
                _audioManager = null;
                _screenReceiver?.Stop();
                _screenReceiver?.Dispose();
                _screenReceiver = null;
                _sharedUdpPeer?.Dispose();
                _sharedUdpPeer = null;
                ConnectButtonContent = "Kết nối";
                ReceivedImage = null;
                PingText = "---";
                BitrateText = "---";
                LossText = "---";
            }
            else
            {
                // Connect logic
                ConnectButtonContent = "Đang kết nối...";
                bool tcpSuccess = await _networkService.ConnectToHostAsync(HostIpAddress);
                if (tcpSuccess)
                {
                    _sharedUdpPeer = new UdpPeer(ClientPort); // Use generated property
                    _screenReceiver = new ScreenReceiver(_sharedUdpPeer);
                    _screenReceiver.OnFrameReady += HandleFrameReady;
                    // TẮT DELAY TẠM THỜI ĐỂ TEST
                    _audioManager = new AudioManager(_sharedUdpPeer, AudioConfig.CreateDefault(), isClientMode: false);
                    _audioManager.StartAudioReceiving();
                    ConnectButtonContent = "Ngắt kết nối";
                    _telemetryTimer.Start();
                }
                else
                {
                    MessageBox.Show($"Không thể kết nối TCP đến Host tại {HostIpAddress}:{NetworkService.HandshakePort}.", "Lỗi Kết Nối TCP", MessageBoxButton.OK, MessageBoxImage.Error);
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
            App.Current.Dispatcher.Invoke(() =>
            {
                ReceivedImage = frameSource;
            });
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