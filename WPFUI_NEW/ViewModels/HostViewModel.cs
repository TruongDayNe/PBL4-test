using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Networking;
using RealTimeUdpStream.Core.Audio;
using RealTimeUdpStream.Core.Input;
using RealTimeUdpStream.Core.ViGEm; // Add ViGEm namespace
using RealTimeUdpStream.Core.Models;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WPFUI_NEW.Services;
using System.Collections.ObjectModel;
using System.Net.Sockets;

namespace WPFUI_NEW.ViewModels
{
    // THÊM: Các class này từ HostPreStreamViewModel
    public partial class ClientListItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _clientIP;
        [ObservableProperty] private string _displayName;
    }
    public partial class ClientRequestViewModel : ClientListItemViewModel { }
    public partial class ConnectedClientViewModel : ClientListItemViewModel
    {
        [ObservableProperty] private int _ping;
        [ObservableProperty] private double _packetLoss;
    }
    public partial class HostViewModel : ObservableObject
    {
        private ScreenProcessor _screenProcessor;
        private ScreenSender _screenSender;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly NetworkService _networkService;

        private UdpPeer _sharedUdpPeer; // Peer chia sẻ
        private AudioManager _audioManager; // Quản lý audio
        private KeyboardManager _keyboardManager; // Quản lý keyboard (WASD → TFGH)
        private ViGEmManager _vigemManager; // Quản lý ViGEm controller (IJKL → Joystick)

        [ObservableProperty] private BitmapSource previewImage = null!; // Initialize non-nullable fields
        [ObservableProperty] private string _streamButtonContent = "Bắt đầu Host";
        [ObservableProperty] private string _statusText = "Sẵn sàng";

        // THÊM: Lấy từ HostPreStreamViewModel
        [ObservableProperty] private string _hostIpAddress = "Đang lấy IP..."; // Cần hàm lấy IP
        public ObservableCollection<ClientRequestViewModel> PendingClients { get; }
        public ObservableCollection<ConnectedClientViewModel> ConnectedClients { get; }

        // THÊM: Commands mới
        public IAsyncRelayCommand AcceptClientCommand { get; }
        public IAsyncRelayCommand RejectClientCommand { get; }
        public IRelayCommand CopyIpCommand { get; }
        public IAsyncRelayCommand KickClientCommand { get; }

        public IAsyncRelayCommand StartStreamCommand { get; }

        public HostViewModel()
        {
            _networkService = new NetworkService();
            _networkService.ClientRequestReceived += OnClientRequestReceived;
            _networkService.ClientAccepted += OnClientAccepted;

            StartStreamCommand = new AsyncRelayCommand(ToggleStreamingAsync);

            // THÊM: Khởi tạo từ HostPreStreamViewModel
            PendingClients = new ObservableCollection<ClientRequestViewModel>();
            ConnectedClients = new ObservableCollection<ConnectedClientViewModel>();

            AcceptClientCommand = new AsyncRelayCommand<ClientRequestViewModel>(AcceptClientAsync);
            RejectClientCommand = new AsyncRelayCommand<ClientRequestViewModel>(RejectClientAsync);
            CopyIpCommand = new RelayCommand(CopyIp);

            KickClientCommand = new AsyncRelayCommand<ConnectedClientViewModel>(KickClientAsync);

            // Thử lấy IP
            LoadHostIp();

            // Initialize non-nullable fields
            _screenProcessor = null!; // Mark as nullable or initialize properly
            _screenSender = null!; // Mark as nullable or initialize properly
            _cancellationTokenSource = null!; // Mark as nullable or initialize properly
            _sharedUdpPeer = null!; // Mark as nullable or initialize properly
            _audioManager = null!; // Mark as nullable or initialize properly
            _keyboardManager = null!; // Mark as nullable or initialize properly
            _vigemManager = null!; // Mark as nullable or initialize properly
        }
        private void LoadHostIp()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                HostIpAddress = ip?.ToString() ?? "Không tìm thấy IP";
            }
            catch (Exception ex)
            {
                HostIpAddress = "Lỗi lấy IP";
                Debug.WriteLine($"Lỗi lấy IP: {ex.Message}");
            }
        }

        private void CopyIp()
        {
            try
            {
                Clipboard.SetText(HostIpAddress);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi copy IP: {ex.Message}");
            }
        }

        // THÊM: Hàm xử lý Accept
        private async Task AcceptClientAsync(ClientRequestViewModel client)
        {
            if (client == null) return;

            try
            {
                // BƯỚC 1: CẬP NHẬT UI TRƯỚC
                // Di chuyển client từ 'Chờ' sang 'Đã kết nối' ngay lập tức
                App.Current.Dispatcher.Invoke(() =>
                {
                    PendingClients.Remove(client);

                    var newConnectedClient = new ConnectedClientViewModel
                    {
                        ClientIP = client.ClientIP,
                        DisplayName = client.DisplayName,
                        Ping = 0,
                        PacketLoss = 0
                    };
                    ConnectedClients.Add(newConnectedClient);
                    UpdateStatusText();
                });

                // BƯỚC 2: BÁO CHO NETWORK SERVICE GỬI "ACCEPT"
                // Việc này sẽ kích hoạt sự kiện OnClientAccepted (để bắt đầu stream),
                // nhưng bây giờ Client đã nằm trong danh sách ConnectedClients.
                await _networkService.AcceptClientAsync(client.ClientIP);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi chấp nhận client {client.ClientIP}: {ex.Message}");
                MessageBox.Show($"Lỗi khi chấp nhận client: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);

                // Rollback (Khôi phục): Nếu có lỗi, xóa client khỏi danh sách kết nối
                App.Current.Dispatcher.Invoke(() =>
                {
                    var clientToRemove = ConnectedClients.FirstOrDefault(c => c.ClientIP == client.ClientIP);
                    if (clientToRemove != null)
                    {
                        ConnectedClients.Remove(clientToRemove);
                    }
                    // (Chúng ta không cần thêm lại vào Pending, Client có thể thử kết nối lại)
                    UpdateStatusText();
                });
            }
        }

        // THÊM: Hàm xử lý Reject
        private async Task RejectClientAsync(ClientRequestViewModel client)
        {
            if (client == null) return;

            try
            {
                await _networkService.RejectClientAsync(client.ClientIP);
                App.Current.Dispatcher.Invoke(() =>
                {
                    PendingClients.Remove(client);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi từ chối client {client.ClientIP}: {ex.Message}");
            }
        }

        // HÀM MỚI: Xử lý khi có client chờ
        private void OnClientRequestReceived(string clientIp, string displayName)
        {
            Debug.WriteLine($"[HostViewModel] Nhận yêu cầu từ {clientIp}");
            // Cần chạy trên luồng UI
            App.Current.Dispatcher.Invoke(() =>
            {
                // Kiểm tra trùng lặp
                if (!PendingClients.Any(c => c.ClientIP == clientIp) && !ConnectedClients.Any(c => c.ClientIP == clientIp))
                {
                    PendingClients.Add(new ClientRequestViewModel
                    {
                        ClientIP = clientIp,
                        DisplayName = $"{displayName} ({clientIp})"
                    });
                }
            });
        }

        // THÊM HÀM MỚI: Xử lý packet "Disconnect"
        private void HandleControlPacket(UdpPacket packet)
        {
            if (packet.Header.PacketType == (byte)UdpPacketType.Disconnect)
            {
                string clientIp = packet.Source.Address.ToString();
                Debug.WriteLine($"[Host] Received DISCONNECT from {clientIp}");

                // Phải chạy trên luồng UI
                App.Current.Dispatcher.Invoke(() =>
                {
                    var clientToRemove = ConnectedClients.FirstOrDefault(c => c.ClientIP == clientIp);
                    if (clientToRemove != null)
                    {
                        ConnectedClients.Remove(clientToRemove);

                        // Cũng phải xóa khỏi ScreenSender
                        var clientEndPoint = new IPEndPoint(packet.Source.Address, 12001); // 12001 là port Client
                        _screenSender?.RemoveClient(clientEndPoint);

                        UpdateStatusText();
                        Debug.WriteLine($"[Host] Client {clientIp} removed from list.");
                    }
                });
            }
            // (Bạn có thể thêm case UdpPacketType.Kick ở đây nếu Client gửi trả)
        }

        private async Task ToggleStreamingAsync()
        {
            if (_screenSender != null)
            {
                // --- LOGIC DỪNG STREAM ---
                if (_sharedUdpPeer != null)
                {
                    _sharedUdpPeer.OnPacketReceived -= HandleControlPacket;
                }

                _cancellationTokenSource?.Cancel();
                _networkService.StopListening();
                Debug.WriteLine("[Host] Đã yêu cầu dừng stream/chờ...");

                _audioManager?.StopAudioStreaming();
                _audioManager?.Dispose();
                _audioManager = null;
                Debug.WriteLine("[Host] AudioManager dừng và hủy.");

                _keyboardManager?.StopSimulation();
                _keyboardManager?.Dispose();
                _keyboardManager = null;
                Debug.WriteLine("[Host] KeyboardManager dừng và hủy.");

                _vigemManager?.StopSimulation(); // Stop ViGEm controller simulation
                _vigemManager?.Dispose();
                _vigemManager = null;
                Debug.WriteLine("[Host] ViGEmManager dừng và hủy.");

                if (_screenSender != null)
                {
                    _screenSender.OnFrameCaptured -= HandleFrameCaptured;
                    _screenSender.Dispose();
                    _screenSender = null;
                }

                _screenProcessor?.Dispose();
                _screenProcessor = null;

                _sharedUdpPeer?.Dispose();
                _sharedUdpPeer = null;
                Debug.WriteLine("[Host] ScreenSender và ScreenProcessor dừng và hủy.");

                StreamButtonContent = "Bắt đầu Host";
                PreviewImage = null;
                StatusText = "Đã dừng. 0 client(s).";
                _cancellationTokenSource = null;

                // Xóa danh sách
                PendingClients.Clear();
                ConnectedClients.Clear();
            }
            else
            {
                // --- LOGIC BẮT ĐẦU HOST ---
                try
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    const int SERVER_PORT = 12000;

                    _sharedUdpPeer = new UdpPeer(SERVER_PORT); // Tạo UdpPeer
                    _sharedUdpPeer.OnPacketReceived += HandleControlPacket;

                    // BAT DAU LANG NGHE UDP - QUAN TRONG!
                    _ = Task.Run(() => _sharedUdpPeer.StartReceivingAsync(), _cancellationTokenSource.Token);
                    Console.WriteLine("[HOST] UdpPeer bat dau lang nghe tren port 12000");
                    Debug.WriteLine("[Host] UdpPeer StartReceivingAsync called.");

                    _screenProcessor = ScreenProcessor.Instance;
                    _screenProcessor.Start();
                    Debug.WriteLine("[Host] ScreenProcessor started.");

                    // Dùng UdpPeer chia sẻ cho ScreenSender
                    _screenSender = new ScreenSender(_sharedUdpPeer, _screenProcessor);
                    _screenSender.OnFrameCaptured += HandleFrameCaptured;
                    Debug.WriteLine("[Host] ScreenSender created.");

                    // HOST mode: không delay, phát ngay
                    _audioManager = new AudioManager(_sharedUdpPeer, AudioConfig.CreateDefault(), isClientMode: false);

                    //_audioManager.StartAudioStreaming(AudioInputType.SystemAudio); // Bắt đầu ghi âm system
                    _audioManager.StartAudioStreaming(AudioInputType.Microphone); // Bắt đầu ghi âm mic

                    Debug.WriteLine("[Host] AudioManager created and started.");

                    // HOST mode = TRUE = SIMULATE (nhận từ CLIENT và giả lập)
                    _keyboardManager = new KeyboardManager(_sharedUdpPeer, isClientMode: true);
                    _keyboardManager.StartSimulation(); // HOST NHẬN và GIẢ LẬP phím WASD → TFGH
                    Console.WriteLine("[HOST] KeyboardManager SIMULATION started - nhan va gia lap phim TFGH");
                    Debug.WriteLine("[Host] KeyboardManager SIMULATION started - se gia lap phim nhan tu CLIENT.");

                    // ViGEm Manager - HOST simulates Xbox 360 controller from IJKL keys
                    _vigemManager = new ViGEmManager(_sharedUdpPeer, isClientMode: false); // HOST = false = SIMULATE
                    _vigemManager.StartSimulation(); // HOST NHẬN IJKL và GIẢ LẬP controller joystick
                    Console.WriteLine("[HOST] ViGEmManager SIMULATION started - nhan IJKL va gia lap controller");
                    Debug.WriteLine("[Host] ViGEmManager SIMULATION started - se gia lap controller tu IJKL.");

                    // Bỏ await để không block UI thread
                    _ = Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    Debug.WriteLine("[Host] SendScreenLoopAsync started.");

                    _ = Task.Run(() => _networkService.StartTcpListenerLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Lỗi nghiêm trọng khi lắng nghe TCP (Port {NetworkService.HandshakePort} có đang được dùng?): {t.Exception?.InnerExceptions.FirstOrDefault()?.Message}", "Lỗi Mạng", MessageBoxButton.OK, MessageBoxImage.Error);
                                // Tự động dừng lại
                                ToggleStreamingAsync();
                            });
                        }
                    }, TaskScheduler.Default);
                    Debug.WriteLine("[Host] StartTcpListenerLoopAsync started.");

                    StatusText = "Đang stream... (0 client(s) connected)";
                    StreamButtonContent = "Dừng STREAM";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi bắt đầu Host: {ex.Message}");
                    _cancellationTokenSource?.Cancel();
                    _screenSender?.Dispose(); _screenSender = null;
                    _screenProcessor?.Dispose(); _screenProcessor = null;
                    StatusText = "Lỗi";
                    StreamButtonContent = "Bắt đầu Host";
                }
            }
        }

        private void OnClientAccepted(string clientIp)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_debug.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] 🔗 OnClientConnected: ClientIP={clientIp}, _audioManager={(_audioManager == null ? "NULL" : "SET")}{Environment.NewLine}");
            
            if (_screenSender == null || _cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                Debug.WriteLine($"[Host] Client {clientIp} đã kết nối, nhưng Host không stream. Bỏ qua.");
                return;
            }

            try
            {
                const int CLIENT_PORT = 12001; // Đây là port Client lắng nghe
                var clientAddress = IPAddress.Parse(clientIp);
                var clientEndPoint = new IPEndPoint(clientAddress, CLIENT_PORT);

                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Setting target endpoint: {clientEndPoint}{Environment.NewLine}");

                // Gửi Video đến client
                _screenSender.AddClient(clientEndPoint);

                // Gửi Audio đến CÙNG client endpoint đó
                _audioManager.SetTargetEndPoint(clientEndPoint);
                
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] ✅ Target endpoint SET for audio!{Environment.NewLine}");

                Debug.WriteLine("[Host] ✅ Client connected - Keyboard simulation already running.");

                App.Current.Dispatcher.Invoke(() =>
                {
                    StatusText = $"Đang stream... ({_screenSender.ClientCount} client(s) connected)";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Lỗi khi thêm client {clientIp}: {ex.Message}");
            }
        }

        // THÊM: Hàm helper cập nhật StatusText
        private void UpdateStatusText()
        {
            StatusText = $"Đang stream... ({ConnectedClients.Count} client(s) connected)";
        }

        private void HandleFrameCaptured(Image frame)
        {
            var bitmapSource = ToBitmapSource(frame);

            // 2. Chỉ đẩy công việc nhẹ (gán) lên luồng UI
            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                PreviewImage = bitmapSource;
            }));

            // 3. Xóa ảnh gốc (vẫn ở luồng nền)
            frame.Dispose();
        }

        // --- Hàm chuyển đổi Image sang BitmapSource ---
        // SỬA LỖI: Khôi phục lại hàm gốc chính xác của bạn
        public static BitmapSource ToBitmapSource(Image image)
        {
            if (image == null) return null;

            // SỬA ĐỔI QUAN TRỌNG:
            // Không tạo 'new Bitmap(image)'! Chỉ cần ép kiểu.
            Bitmap bitmap = image as Bitmap;
            if (bitmap == null)
            {
                Debug.WriteLine("Lỗi ToBitmapSource: Ảnh nhận được không phải là Bitmap.");
                return null; // Không thể xử lý nếu không phải là Bitmap
            }

            System.Drawing.Imaging.BitmapData bitmapData = null;
            try
            {
                // Khóa bits của ảnh GỐC (đang được ReadLock từ ScreenSender)
                bitmapData = bitmap.LockBits(
                  new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                  System.Drawing.Imaging.ImageLockMode.ReadOnly,
                  bitmap.PixelFormat); // SỬA ĐỔI: Dùng PixelFormat của bitmap gốc

                // Kiểm tra xem pixel format có phải là 32bpp hay không
                // (Nếu không, bạn cần logic chuyển đổi, nhưng giả sử nó là 32bpp)
                var pixelFormat = System.Windows.Media.PixelFormats.Bgr32;
                if (bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppRgb && bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                {
                    // Nếu format không đúng, hàm Create có thể thất bại
                    // Tạm thời vẫn dùng Bgr32
                    Debug.WriteLine("Cảnh báo: PixelFormat của ảnh gốc không phải là 32bpp.");
                }


                var bitmapSource = BitmapSource.Create(
                  bitmapData.Width, bitmapData.Height,
                  bitmap.HorizontalResolution, bitmap.VerticalResolution,
                  pixelFormat, // Giả định là Bgr32
                  null,
                  bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

                bitmapSource.Freeze(); // Quan trọng cho đa luồng
                return bitmapSource;
            }
            catch (Exception ex)
            {
                // Lỗi "Object is currently in use elsewhere" thường xảy ra ở đây
                // nếu LockBits bị gọi sai.
                Debug.WriteLine($"Lỗi ToBitmapSource: {ex.Message}");
                return null;
            }
            finally
            {
                if (bitmapData != null)
                {
                    // Luôn mở khóa bits
                    bitmap?.UnlockBits(bitmapData);
                }
            }
        }

        private async Task KickClientAsync(ConnectedClientViewModel client)
        {
            if (client == null) return;

            // Hiển thị hộp thoại xác nhận
            var result = MessageBox.Show($"Bạn có chắc muốn đuổi (kick) client: {client.DisplayName}?",
                                         "Xác nhận Kick",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.No) return;

            Debug.WriteLine($"[Host] Đang kick client: {client.ClientIP}");

            // TODO: Gửi tin nhắn "KICK" qua mạng
            // (Chúng ta sẽ làm việc này ở Bước 1 - Thêm tính năng "Kick" như đã bàn)

            // Tạm thời chỉ xóa khỏi danh sách
            try
            {
                // BƯỚC 1: Lấy thông tin client
                var clientIp = IPAddress.Parse(client.ClientIP);
                var clientEndPoint = new IPEndPoint(clientIp, 12001); // 12001 là port Client lắng nghe

                // BƯỚC 2: Ngừng gửi stream cho client
                _screenSender?.RemoveClient(clientEndPoint);

                // BƯỚC 3: Gửi tin nhắn "Kick" qua mạng
                var kickPacket = new UdpPacket(UdpPacketType.Kick, 0);
                await _sharedUdpPeer.SendToAsync(kickPacket, clientEndPoint);

                // BƯỚC 4: Xóa khỏi giao diện UI
                App.Current.Dispatcher.Invoke(() =>
                {
                    ConnectedClients.Remove(client);
                    UpdateStatusText();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Lỗi khi Kick: {ex.Message}");
                MessageBox.Show($"Lỗi khi kick client: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            // Nếu đang stream, gọi Toggle để dừng và dọn dẹp
            if (_screenSender != null)
            {
                ToggleStreamingAsync().Wait(); // Chờ 
            }
        }
    }
}