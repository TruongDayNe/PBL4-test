using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Networking;
using RealTimeUdpStream.Core.Audio;
using RealTimeUdpStream.Core.Input;
using RealTimeUdpStream.Core.ViGEm; // Add ViGEm namespace
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
        private System.Net.IPEndPoint _hostEndPoint;
        private AudioManager _audioManager;
        private KeyboardManager _keyboardManager; // Quản lý keyboard (WASD)
        private ViGEmManager _vigemManager; // Quản lý ViGEm controller (IJKL)

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
            _keyboardManager = null!; // Mark as nullable or initialize properly
            _vigemManager = null!; // Mark as nullable or initialize properly
            _receivedImage = null!; // Mark as nullable or initialize properly

            // Initialize telemetry timer
            _telemetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _telemetryTimer.Tick += OnTelemetryTick;
        }
        private void HandleControlPacket(UdpPacket packet)
        {
            if (packet.Header.PacketType == (byte)UdpPacketType.Kick)
            {
                Debug.WriteLine($"[Client] Received KICK from Host!");

                // Chạy trên luồng UI
                App.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Bạn đã bị Host ngắt kết nối.", "Bị Kick", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Gọi hàm ngắt kết nối
                    // (Chúng ta không cần await, chỉ cần nó chạy)
                    _ = ToggleConnectionAsync();
                });
            }
        }

        private async Task ToggleConnectionAsync()
        {
            if (_screenReceiver != null)
            {

                if (_sharedUdpPeer != null && _hostEndPoint != null)
                {
                    try
                    {
                        Debug.WriteLine($"[Client] Sending DISCONNECT to {_hostEndPoint}");
                        var disconnectPacket = new UdpPacket(UdpPacketType.Disconnect, 0);
                        await _sharedUdpPeer.SendToAsync(disconnectPacket, _hostEndPoint);
                        Debug.WriteLine($"[Client] DISCONNECT sent.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Client] Error sending DISCONNECT: {ex.Message}");
                    }
                }

                if (_sharedUdpPeer != null)
                {
                    _sharedUdpPeer.OnPacketReceived -= HandleControlPacket;
                }

                // --- DISCONNECT LOGIC (Giữ nguyên) ---
                _telemetryTimer.Stop();
                _audioManager?.StopAudioReceiving();
                _audioManager?.Dispose();
                _audioManager = null;
                _keyboardManager?.StopCapture();
                _keyboardManager?.Dispose();
                _keyboardManager = null;
                _vigemManager?.StopCapture();
                _vigemManager?.Dispose();
                _vigemManager = null;
                _screenReceiver?.Stop();
                _screenReceiver?.Dispose();
                _screenReceiver = null;
                _sharedUdpPeer?.Dispose();
                _sharedUdpPeer = null;
                _hostEndPoint = null;
                ConnectButtonContent = "Kết nối";
                ReceivedImage = null;
                PingText = "---";
                BitrateText = "---";
                LossText = "---";
            }
            else
            {
                // --- CONNECT LOGIC (Đã sửa) ---
                // Việc handshake TCP đã được ClientConnectViewModel thực hiện.
                // Hàm này giờ chỉ để BẮT ĐẦU STREAM UDP.
                try
                {
                    ConnectButtonContent = "Đang kết nối (UDP)...";

                    _sharedUdpPeer = new UdpPeer(ClientPort);

                    _sharedUdpPeer.OnPacketReceived += HandleControlPacket;

                    _screenReceiver = new ScreenReceiver(_sharedUdpPeer);
                    _screenReceiver.OnFrameReady += HandleFrameReady;

                    _audioManager = new AudioManager(_sharedUdpPeer, AudioConfig.CreateDefault(), isClientMode: false);
                    _audioManager.StartAudioReceiving();

                    // Lấy HOST endpoint để gửi phím
                    var hostEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(HostIpAddress), 12000);
                    _hostEndPoint = hostEndPoint;

                    // Keyboard Manager - WASD keys
                    _keyboardManager = new KeyboardManager(_sharedUdpPeer, isClientMode: false);
                    _keyboardManager.SetTargetEndPoint(hostEndPoint);
                    _keyboardManager.StartCapture();
                    Console.WriteLine("[CLIENT] KeyboardManager CAPTURE started - gui phim WASD cho HOST");
                    Debug.WriteLine("[Client] KeyboardManager CAPTURE started - se gui phim WASD cho HOST.");

                    // ViGEm Manager - IJKL keys for controller
                    _vigemManager = new ViGEmManager(_sharedUdpPeer, isClientMode: true);
                    _vigemManager.SetTargetEndPoint(hostEndPoint);
                    _vigemManager.StartCapture();
                    Console.WriteLine("[CLIENT] ViGEmManager CAPTURE started - gui phim IJKL cho HOST");
                    Debug.WriteLine("[Client] ViGEmManager CAPTURE started - se gui phim IJKL cho HOST.");

                    ConnectButtonContent = "Ngắt kết nối";
                    _telemetryTimer.Start();

                    // Cần return Task.CompletedTask vì hàm là async
                    await Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    // Lỗi này là lỗi khi BẮT ĐẦU UDP (ví dụ: port 12001 đã dùng)
                    MessageBox.Show($"Lỗi khi bắt đầu lắng nghe (UDP): {ex.Message}", "Lỗi UDP", MessageBoxButton.OK, MessageBoxImage.Error);
                    ConnectButtonContent = "Kết nối";
                }
            }
        }


        // --- ĐƯỢC GỌI MỖI GIÂY ---
        private void OnTelemetryTick(object sender, EventArgs e)
        {
            if (_sharedUdpPeer == null) return; // Kiểm tra _sharedUdpPeer
                                                // 
            if (_hostEndPoint != null)
            {
                var pingPacket = new UdpPacket(UdpPacketType.Ping, 0);
                // Gửi và không cần chờ (fire-and-forget)
                _ = _sharedUdpPeer.SendToAsync(pingPacket, _hostEndPoint);
            }

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

            // THÊM: Đảm bảo hủy đăng ký nếu chưa
            if (_sharedUdpPeer != null)
            {
                _sharedUdpPeer.OnPacketReceived -= HandleControlPacket;
            }

        }
    }
}