using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Networking;
using RealTimeUdpStream.Core.Audio;
using RealTimeUdpStream.Core.Input;
using RealTimeUdpStream.Core.ViGEm;
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
using System.Windows.Threading;
using WPFUI_NEW.Views;

namespace WPFUI_NEW.ViewModels
{
    public partial class ClientListItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _clientIP = "";
        [ObservableProperty] private string _displayName = "";
    }
    public partial class ClientRequestViewModel : ClientListItemViewModel { }
    public partial class ConnectedClientViewModel : ClientListItemViewModel
    {
        [ObservableProperty] private int _ping;
        [ObservableProperty] private double _packetLoss;
        [ObservableProperty] private bool _isMuted;
    }

    public partial class HostViewModel : ObservableObject
    {
        // --- CÁC THÀNH PHẦN CORE ---
        private ScreenProcessor? _screenProcessor;
        private ScreenSender? _screenSender;
        private readonly NetworkService _networkService;

        // Tách riêng Token cho TCP (luôn chạy) và Stream (chạy khi bấm nút)
        private CancellationTokenSource? _tcpCts;
        private CancellationTokenSource? _streamCts;

        private UdpPeer? _sharedUdpPeer;
        private AudioManager? _audioManager;
        private KeyboardManager? _keyboardManager;
        private ViGEmManager? _vigemManager;

        // --- UI PROPERTIES ---
        [ObservableProperty] private BitmapSource? previewImage;
        [ObservableProperty] private string _streamButtonContent = "BẮT ĐẦU STREAM";
        [ObservableProperty] private string _statusText = "Đang chờ kết nối...";
        [ObservableProperty] private string _hostIpAddress = "Đang lấy IP...";

        // --- COLLECTIONS ---
        public ObservableCollection<ClientRequestViewModel> PendingClients { get; }
        public ObservableCollection<ConnectedClientViewModel> ConnectedClients { get; }

        // --- COMMANDS ---
        public IAsyncRelayCommand AcceptClientCommand { get; }
        public IAsyncRelayCommand RejectClientCommand { get; }
        public IRelayCommand CopyIpCommand { get; }
        public IAsyncRelayCommand KickClientCommand { get; }
        public IAsyncRelayCommand StartStreamCommand { get; }
        public IRelayCommand ToggleOverlayCommand { get; }

        // Command mới cho F1/F2
        public IAsyncRelayCommand AcceptLatestRequestCommand { get; }
        public IAsyncRelayCommand RejectLatestRequestCommand { get; }

        // --- OVERLAY PROPERTIES ---
        private HostOverlayWindow _overlayWindow;
        private DispatcherTimer _statsTimer;

        [ObservableProperty] private bool _isOverlayVisible = true;
        [ObservableProperty] private string _hostBitrateText = "0.0";
        [ObservableProperty] private int _clientCount = 0;

        // Toast Notification
        [ObservableProperty] private string _toastMessage = "";
        [ObservableProperty] private bool _isToastVisible = false;
        [ObservableProperty] private string _toastKeyHint = "";

        // Token source để quản lý việc ẩn Toast
        private CancellationTokenSource? _toastCts;

        // Biến lưu request mới nhất để xử lý bằng F1/F2
        private ClientRequestViewModel? _latestRequest;

        private DispatcherTimer _previewTimer;

        // THÊM: Command Tắt Mic
        public IAsyncRelayCommand<ConnectedClientViewModel> ToggleMuteClientCommand { get; }

        public HostViewModel()
        {
            _networkService = new NetworkService();
            _networkService.ClientRequestReceived += OnClientRequestReceived;
            _networkService.ClientAccepted += OnClientAccepted;

            StartStreamCommand = new AsyncRelayCommand(ToggleStreamingAsync);

            PendingClients = new ObservableCollection<ClientRequestViewModel>();
            ConnectedClients = new ObservableCollection<ConnectedClientViewModel>();

            AcceptClientCommand = new AsyncRelayCommand<ClientRequestViewModel>(AcceptClientAsync);
            RejectClientCommand = new AsyncRelayCommand<ClientRequestViewModel>(RejectClientAsync);
            // Thêm Command Mute
            ToggleMuteClientCommand = new AsyncRelayCommand<ConnectedClientViewModel>(ToggleMuteClientAsync);

            // F1/F2 Commands
            AcceptLatestRequestCommand = new AsyncRelayCommand(async () => await AcceptClientAsync(_latestRequest));
            RejectLatestRequestCommand = new AsyncRelayCommand(async () => await RejectClientAsync(_latestRequest));

            CopyIpCommand = new RelayCommand(CopyIp);
            KickClientCommand = new AsyncRelayCommand<ConnectedClientViewModel>(KickClientAsync);
            ToggleOverlayCommand = new RelayCommand(ToggleOverlay);

            LoadHostIp();

            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statsTimer.Tick += OnStatsTick;

            // === LOGIC PREVIEW MỚI ===
            // 1. Khởi động ScreenProcessor ngay lập tức (không đợi bấm Stream)
            try
            {
                _screenProcessor = ScreenProcessor.Instance;
                _screenProcessor.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khởi động ScreenProcessor: {ex.Message}");
            }

            // 2. Tạo Timer để update Preview (chạy 10 lần/giây khi CHƯA stream)
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _previewTimer.Tick += OnPreviewTick;
            _previewTimer.Start();


        }

        // Hàm xử lý Preview Timer
        private void OnPreviewTick(object sender, EventArgs e)
        {
            // Chỉ chạy khi chưa có ScreenSender (tức là chưa Stream)
            if (_screenSender == null && _screenProcessor != null)
            {
                _screenProcessor.ProcessScreenImage(img =>
                {
                    // Copy ảnh để hiển thị lên UI
                    // Lưu ý: Hàm này chạy trên thread của Timer (UI Thread) nếu dùng DispatcherTimer,
                    // nhưng ProcessScreenImage có thể gọi callback từ thread khác.
                    // Để an toàn, ta vẫn dùng Dispatcher.
                    var source = ToBitmapSource(img);
                    if (source != null)
                    {
                        App.Current.Dispatcher.Invoke(() => PreviewImage = source);
                    }
                });
            }
        }

        // Xử lý Mute Client
        private async Task ToggleMuteClientAsync(ConnectedClientViewModel? client)
        {
            if (client == null) return;

            client.IsMuted = !client.IsMuted; // Đổi trạng thái UI

            // Cập nhật xuống AudioManager
            if (_audioManager != null)
            {
                _audioManager.MuteClient(client.ClientIP, client.IsMuted);
            }

            // (Tùy chọn) Có thể gửi packet báo cho Client biết họ bị mute nếu muốn
            await Task.CompletedTask;
        }

        // --- LOGIC HIỂN THỊ TOAST (SỬA ĐỔI) ---
        private async void ShowToast(string message, string keyHint = "", bool isSticky = false)
        {
            // 1. Hủy timer ẩn cũ nếu đang chạy (để tránh nó tắt nhầm thông báo mới)
            _toastCts?.Cancel();
            _toastCts = new CancellationTokenSource();
            var token = _toastCts.Token;

            // 2. Hiển thị thông báo mới
            ToastMessage = message;
            ToastKeyHint = keyHint;
            IsToastVisible = true;

            // 3. Nếu KHÔNG phải thông báo dính (sticky), thì hẹn giờ tắt
            if (!isSticky)
            {
                try
                {
                    await Task.Delay(3000, token); // Chờ 3s
                    if (!token.IsCancellationRequested)
                    {
                        App.Current.Dispatcher.Invoke(() => IsToastVisible = false);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Bỏ qua nếu bị hủy (do có toast mới chèn vào hoặc user tắt)
                }
            }
            // Nếu là isSticky = true, code sẽ dừng ở đây và Toast vẫn hiện mãi cho đến khi được tắt thủ công.
        }

        private void ToggleOverlay()
        {
            IsOverlayVisible = !IsOverlayVisible;
            if (!IsOverlayVisible)
            {
                // Thông báo này tự tắt sau 3s (isSticky = false mặc định)
                ShowToast("Đã ẩn Dashboard", "Ctrl + H", isSticky: false);
            }
        }

        private void OnClientRequestReceived(string clientIp, string displayName)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (!PendingClients.Any(c => c.ClientIP == clientIp) && !ConnectedClients.Any(c => c.ClientIP == clientIp))
                {
                    var newRequest = new ClientRequestViewModel
                    {
                        ClientIP = clientIp,
                        DisplayName = $"{displayName} ({clientIp})"
                    };
                    PendingClients.Add(newRequest);

                    _latestRequest = newRequest;

                    // HIỆN TOAST DÍNH (Sticky) - Không tự tắt
                    ShowToast($"Yêu cầu: {displayName}", "F1: Duyệt | F2: Hủy", isSticky: true);

                    if (_overlayWindow != null) IsOverlayVisible = true;
                }
            });
        }

        // --- CÁC LOGIC KHÁC GIỮ NGUYÊN ---

        public void StartTcpListening()
        {
            if (_tcpCts != null) return; // Đã chạy rồi thì không chạy lại

            _tcpCts = new CancellationTokenSource();

            // Chạy loop lắng nghe TCP
            _ = Task.Run(() => _networkService.StartTcpListenerLoopAsync(_tcpCts.Token))
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var ex = t.Exception?.InnerException;

                        // Vẫn giữ logic kiểm tra cổng bận để an toàn
                        if (ex is SocketException socketEx && socketEx.SocketErrorCode == SocketError.AddressAlreadyInUse)
                        {
                            Debug.WriteLine("[HostViewModel] Cổng TCP đã bị chiếm.");
                            App.Current.Dispatcher.Invoke(() =>
                                MessageBox.Show("Cổng kết nối (12345) đang bị ứng dụng khác chiếm dụng!", "Không thể làm Host"));
                        }
                        else
                        {
                            App.Current.Dispatcher.Invoke(() =>
                                MessageBox.Show($"Lỗi TCP Listener: {ex?.Message}"));
                        }
                    }
                });

            StatusText = "Đã mở cổng kết nối. Sẵn sàng nhận Client.";
        }

        private void OnStatsTick(object sender, EventArgs e)
        {
            if (_sharedUdpPeer != null)
            {
                var snapshot = _sharedUdpPeer.Stats.GetSnapshot();
                HostBitrateText = (snapshot.SentBitrateKbps / 1024.0).ToString("F1");
                ClientCount = ConnectedClients.Count;
            }
        }

        private void LoadHostIp()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                HostIpAddress = ip?.ToString() ?? "Không tìm thấy IP";
            }
            catch (Exception ex) { HostIpAddress = "Lỗi lấy IP"; }
        }

        private void CopyIp()
        {
            try { Clipboard.SetText(HostIpAddress); } catch { }
        }

        private async Task AcceptClientAsync(ClientRequestViewModel? client)
        {
            if (client == null) return;

            // Tắt Toast ngay lập tức
            _toastCts?.Cancel();
            App.Current.Dispatcher.Invoke(() => IsToastVisible = false);

            try
            {
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

                await _networkService.AcceptClientAsync(client.ClientIP);
                _latestRequest = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi chấp nhận: {ex.Message}");
            }
        }

        private async Task RejectClientAsync(ClientRequestViewModel? client)
        {
            if (client == null) return;

            // Tắt Toast ngay lập tức
            _toastCts?.Cancel();
            App.Current.Dispatcher.Invoke(() => IsToastVisible = false);

            try
            {
                await _networkService.RejectClientAsync(client.ClientIP);
                App.Current.Dispatcher.Invoke(() => PendingClients.Remove(client));
                _latestRequest = null;
            }
            catch (Exception ex) { Debug.WriteLine($"Lỗi từ chối: {ex.Message}"); }
        }

        private void HandleControlPacket(UdpPacket packet)
        {
            if (packet.Header.PacketType == (byte)UdpPacketType.Disconnect)
            {
                string clientIp = packet.Source.Address.ToString();
                App.Current.Dispatcher.Invoke(() =>
                {
                    var client = ConnectedClients.FirstOrDefault(c => c.ClientIP == clientIp);
                    if (client != null)
                    {
                        ConnectedClients.Remove(client);
                        var ep = new IPEndPoint(packet.Source.Address, 12001);
                        _screenSender?.RemoveClient(ep);
                        UpdateStatusText();
                    }
                });
            }
        }

        private async Task ToggleStreamingAsync()
        {
            if (_screenSender != null)
            {
                if (_sharedUdpPeer != null) _sharedUdpPeer.OnPacketReceived -= HandleControlPacket;

                _streamCts?.Cancel();

                _audioManager?.StopAudioStreaming(); _audioManager?.Dispose(); _audioManager = null;
                _keyboardManager?.StopSimulation(); _keyboardManager?.Dispose(); _keyboardManager = null;
                _vigemManager?.StopSimulation(); _vigemManager?.Dispose(); _vigemManager = null;

                if (_screenSender != null)
                {
                    _screenSender.OnFrameCaptured -= HandleFrameCaptured;
                    _screenSender.Dispose(); _screenSender = null;
                }

                _sharedUdpPeer?.Dispose(); _sharedUdpPeer = null;

                StreamButtonContent = "BẮT ĐẦU STREAM";
                UpdateStatusText();

                _streamCts = null;
                _previewTimer.Start();

                _statsTimer.Stop();
                App.Current.Dispatcher.Invoke(() =>
                {
                    _overlayWindow?.Close();
                    _overlayWindow = null;
                });
            }
            else
            {
                // QUAN TRỌNG: TẮT PREVIEW TIMER (Để ScreenSender lo việc update ảnh mượt hơn)
                _previewTimer.Stop();
                try
                {
                    _streamCts = new CancellationTokenSource();
                    const int SERVER_PORT = 12000;

                    _sharedUdpPeer = new UdpPeer(SERVER_PORT);
                    _sharedUdpPeer.OnPacketReceived += HandleControlPacket;
                    _ = Task.Run(() => _sharedUdpPeer.StartReceivingAsync(), _streamCts.Token);

                    _screenProcessor = ScreenProcessor.Instance;
                    _screenProcessor.Start();

                    _screenSender = new ScreenSender(_sharedUdpPeer, _screenProcessor);
                    _screenSender.OnFrameCaptured += HandleFrameCaptured;

                    foreach (var connectedClient in ConnectedClients)
                    {
                        var ep = new IPEndPoint(IPAddress.Parse(connectedClient.ClientIP), 12001);
                        _screenSender.AddClient(ep);
                    }
                    if (_screenProcessor == null) _screenProcessor = ScreenProcessor.Instance;

                    _audioManager = new AudioManager(_sharedUdpPeer, AudioConfig.CreateDefault(), false);
                    _audioManager.StartAudioStreaming(AudioInputType.SystemAudio);

                    _keyboardManager = new KeyboardManager(_sharedUdpPeer, true);
                    _keyboardManager.StartSimulation();

                    _vigemManager = new ViGEmManager(_sharedUdpPeer, false);
                    _vigemManager.StartSimulation();
                    if (_audioManager != null)
                    {
                        foreach (var client in ConnectedClients)
                        {
                            if (client.IsMuted) _audioManager.MuteClient(client.ClientIP, true);
                        }
                    }

                    _ = Task.Run(() => _screenSender.SendScreenLoopAsync(_streamCts.Token), _streamCts.Token);

                    StreamButtonContent = "DỪNG STREAM";
                    UpdateStatusText();

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _overlayWindow = new HostOverlayWindow();
                        _overlayWindow.DataContext = this;
                        _overlayWindow.Show();
                    });
                    _statsTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi Start Stream: {ex.Message}");
                    _streamCts?.Cancel();
                    StreamButtonContent = "BẮT ĐẦU STREAM";
                }
            }
        }

        private void OnClientAccepted(string clientIp)
        {
            if (_screenSender != null && _streamCts != null && !_streamCts.IsCancellationRequested)
            {
                try
                {
                    var ep = new IPEndPoint(IPAddress.Parse(clientIp), 12001);
                    _screenSender.AddClient(ep);
                    _audioManager?.SetTargetEndPoint(ep);
                }
                catch { }
            }
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (_screenSender != null)
                StatusText = $"Đang Livestream... ({ConnectedClients.Count} đang xem)";
            else
                StatusText = $"Chế độ chờ. ({ConnectedClients.Count} client đã kết nối)";
        }

        private void HandleFrameCaptured(Image frame)
        {
            var source = ToBitmapSource(frame);
            if (source != null) App.Current.Dispatcher.BeginInvoke(() => PreviewImage = source);
            frame.Dispose();
        }

        public static BitmapSource? ToBitmapSource(Image image)
        {
            if (image == null) return null;
            Bitmap? bitmap = image as Bitmap;
            if (bitmap == null) return null;

            System.Drawing.Imaging.BitmapData? data = null;
            try
            {
                data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
                var src = BitmapSource.Create(data.Width, data.Height, bitmap.HorizontalResolution, bitmap.VerticalResolution, System.Windows.Media.PixelFormats.Bgr32, null, data.Scan0, data.Stride * data.Height, data.Stride);
                src.Freeze();
                return src;
            }
            catch { return null; }
            finally { if (data != null) bitmap.UnlockBits(data); }
        }

        private async Task KickClientAsync(ConnectedClientViewModel? client)
        {
            if (client == null) return;
            if (MessageBox.Show($"Kick {client.DisplayName}?", "Kick", MessageBoxButton.YesNo) == MessageBoxResult.No) return;

            try
            {
                var ep = new IPEndPoint(IPAddress.Parse(client.ClientIP), 12001);
                _screenSender?.RemoveClient(ep);
                if (_sharedUdpPeer != null) await _sharedUdpPeer.SendToAsync(new UdpPacket(UdpPacketType.Kick, 0), ep);

                App.Current.Dispatcher.Invoke(() =>
                {
                    ConnectedClients.Remove(client);
                    UpdateStatusText();
                });
            }
            catch { }
        }

        public void Cleanup()
        {
            _tcpCts?.Cancel();
            if (_screenSender != null) ToggleStreamingAsync().Wait();
        }
    }
}