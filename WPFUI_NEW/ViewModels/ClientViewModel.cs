using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Networking;
using RealTimeUdpStream.Core.Audio;
using RealTimeUdpStream.Core.Input;
using RealTimeUdpStream.Core.Models; // Thêm using cho TelemetrySnapshot
using RealTimeUdpStream.Core.Networking; // Thêm using cho NetworkStats
using RealTimeUdpStream.Core.ViGEm; // Add ViGEm namespace
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows; // Thêm using này cho MessageBox
using System.Windows.Automation;
using System.Windows.Input;
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

        // === CÁC THUỘC TÍNH UI MỚI (Overlay Style) ===
        [ObservableProperty] private bool _isMenuVisible = true; // Trạng thái thanh Bar
        [ObservableProperty] private bool _isStatsVisible = false; // Trạng thái bảng Ping/FPS

        // Toast Notification
        [ObservableProperty] private string _toastMessage = "";
        [ObservableProperty] private bool _isToastVisible = false;
        [ObservableProperty] private string _toastKeyHint = ""; // Ví dụ: "Ctrl + M"
        [ObservableProperty] private string _fpsText = "0 FPS";
        [ObservableProperty] private string _fecText = "0 pkts";
        private int _receivedFrameCount = 0;

        // === COMMANDS MỚI ===
        public IRelayCommand ToggleMenuCommand { get; }
        public IRelayCommand ToggleStatsCommand { get; }

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
        public ICommand BackToMenuCommand { get; }

        // Sửa Constructor
        public ClientViewModel(ICommand backToMenuCommand)
        {
            // Logic quay về: Cleanup -> Chuyển View
            BackToMenuCommand = new RelayCommand(() =>
            {
                Cleanup(); // Ngắt kết nối UDP, dừng receiver
                backToMenuCommand.Execute(null);
            });
            _networkService = new NetworkService();
            ConnectCommand = new AsyncRelayCommand(ToggleConnectionAsync);
            ToggleMenuCommand = new RelayCommand(ToggleMenu);
            ToggleStatsCommand = new RelayCommand(ToggleStats);

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

        // === LOGIC XỬ LÝ GIAO DIỆN ===

        private void ToggleMenu()
        {
            IsMenuVisible = !IsMenuVisible;
            if (!IsMenuVisible)
            {
                ShowToast("Đã ẩn Menu", "Ctrl + M");
            }
        }

        private void ToggleStats()
        {
            IsStatsVisible = !IsStatsVisible;
            ShowToast(IsStatsVisible ? "Đã bật Overlay Thông số" : "Đã tắt Overlay Thông số");
        }

        // Hàm hiển thị thông báo tự tắt sau 2.5s
        private async void ShowToast(string message, string keyHint = "")
        {
            ToastMessage = message;
            ToastKeyHint = keyHint;
            IsToastVisible = true;

            // Đợi 2.5 giây rồi ẩn (chạy trên thread pool để không block UI)
            await Task.Delay(2500);

            // Quay lại luồng UI để ẩn
            App.Current.Dispatcher.Invoke(() =>
            {
                // Chỉ ẩn nếu message chưa bị thay đổi bởi toast mới
                if (ToastMessage == message)
                {
                    IsToastVisible = false;
                }
            });
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
                    Console.WriteLine("[CLIENT] ===== STARTING UDP CONNECTION =====");
                    ConnectButtonContent = "Đang kết nối (UDP)...";

                    Console.WriteLine("[CLIENT] Creating UdpPeer on port " + ClientPort);
                    _sharedUdpPeer = new UdpPeer(ClientPort);

                    Console.WriteLine("[CLIENT] Subscribing to OnPacketReceived");
                    _sharedUdpPeer.OnPacketReceived += HandleControlPacket;

                    Console.WriteLine("[CLIENT] Creating ScreenReceiver");
                    _screenReceiver = new ScreenReceiver(_sharedUdpPeer);
                    _screenReceiver.OnFrameReady += HandleFrameReady;

                    Console.WriteLine("[CLIENT] Creating AudioManager");
                    // AUDIO DISABLED
                    //_audioManager = new AudioManager(_sharedUdpPeer, AudioConfig.CreateDefault(), isClientMode: false);
                    //_audioManager.StartAudioReceiving();

                    // Lấy HOST endpoint để gửi phím
                    var hostEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(HostIpAddress), 12000);
                    _hostEndPoint = hostEndPoint;
                    Console.WriteLine($"[CLIENT] Host endpoint: {hostEndPoint}");

                    // Keyboard Manager - WASD keys
                    Console.WriteLine("[CLIENT] Creating KeyboardManager...");
                    _keyboardManager = new KeyboardManager(_sharedUdpPeer, isClientMode: true); // CLIENT capture phím
                    Console.WriteLine("[CLIENT] Setting target endpoint...");
                    _keyboardManager.SetTargetEndPoint(hostEndPoint);
                    Console.WriteLine("[CLIENT] Calling StartCapture...");
                    _keyboardManager.StartCapture();
                    Console.WriteLine("[CLIENT] KeyboardManager CAPTURE started - gui phim WASD cho HOST");
                    Debug.WriteLine("[Client] KeyboardManager CAPTURE started - se gui phim WASD cho HOST.");

                    // ViGEm Manager - IJKL keys for controller
                    _vigemManager = new ViGEmManager(_sharedUdpPeer, isClientMode: false); // CLIENT capture IJKL
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
            if (_sharedUdpPeer == null) return;

            // Gửi Ping định kỳ
            if (_hostEndPoint != null)
            {
                var pingPacket = new UdpPacket(UdpPacketType.Ping, 0);
                // Timestamp được tự động gán trong Constructor của UdpPacket
                _ = _sharedUdpPeer.SendToAsync(pingPacket, _hostEndPoint);
            }

            // Lấy thông số từ Stats
            var snapshot = _sharedUdpPeer.Stats.GetSnapshot();

            // Luôn cập nhật UI từ Snapshot (không cần check "---" nữa)
            PingText = $"{snapshot.Rtt.TotalMilliseconds:F0} ms";
            BitrateText = $"{snapshot.ReceivedBitrateKbps} Kbps";
            LossText = $"{snapshot.PacketLossRate:F1} %";
            FecText = $"{snapshot.FecPacketsRecoveredPerSec} pkts";

            int currentFps = System.Threading.Interlocked.Exchange(ref _receivedFrameCount, 0);
            FpsText = $"{currentFps} FPS";
        }

        private void HandleFrameReady(BitmapSource frameSource)
        {
            // Logic y hệt HostViewModel:
            System.Threading.Interlocked.Increment(ref _receivedFrameCount);
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