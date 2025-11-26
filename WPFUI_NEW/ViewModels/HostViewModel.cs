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
    }

    public partial class HostViewModel : ObservableObject
    {
        // Đánh dấu các field này là nullable (?) để tránh warning khi chưa khởi tạo
        private ScreenProcessor? _screenProcessor;
        private ScreenSender? _screenSender;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly NetworkService _networkService;

        private UdpPeer? _sharedUdpPeer;
        private AudioManager? _audioManager;
        private KeyboardManager? _keyboardManager;
        private ViGEmManager? _vigemManager;

        // Initialize non-nullable properties with default values or allow null
        [ObservableProperty] private BitmapSource? previewImage;
        [ObservableProperty] private string _streamButtonContent = "Bắt đầu Host";
        [ObservableProperty] private string _statusText = "Sẵn sàng";
        [ObservableProperty] private string _hostIpAddress = "Đang lấy IP...";

        public ObservableCollection<ClientRequestViewModel> PendingClients { get; }
        public ObservableCollection<ConnectedClientViewModel> ConnectedClients { get; }

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

            PendingClients = new ObservableCollection<ClientRequestViewModel>();
            ConnectedClients = new ObservableCollection<ConnectedClientViewModel>();

            AcceptClientCommand = new AsyncRelayCommand<ClientRequestViewModel>(AcceptClientAsync);
            RejectClientCommand = new AsyncRelayCommand<ClientRequestViewModel>(RejectClientAsync);
            CopyIpCommand = new RelayCommand(CopyIp);
            KickClientCommand = new AsyncRelayCommand<ConnectedClientViewModel>(KickClientAsync);

            LoadHostIp();
        }

        private void LoadHostIp()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                // Sửa CS8600: Dùng ?. để tránh null
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

        private async Task AcceptClientAsync(ClientRequestViewModel? client)
        {
            if (client == null) return;

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi chấp nhận client {client.ClientIP}: {ex.Message}");
                MessageBox.Show($"Lỗi khi chấp nhận client: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);

                App.Current.Dispatcher.Invoke(() =>
                {
                    var clientToRemove = ConnectedClients.FirstOrDefault(c => c.ClientIP == client.ClientIP);
                    if (clientToRemove != null)
                    {
                        ConnectedClients.Remove(clientToRemove);
                    }
                    UpdateStatusText();
                });
            }
        }

        private async Task RejectClientAsync(ClientRequestViewModel? client)
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

        private void OnClientRequestReceived(string clientIp, string displayName)
        {
            Debug.WriteLine($"[HostViewModel] Nhận yêu cầu từ {clientIp}");
            App.Current.Dispatcher.Invoke(() =>
            {
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

        private void HandleControlPacket(UdpPacket packet)
        {
            if (packet.Header.PacketType == (byte)UdpPacketType.Disconnect)
            {
                string clientIp = packet.Source.Address.ToString();
                Debug.WriteLine($"[Host] Received DISCONNECT from {clientIp}");

                App.Current.Dispatcher.Invoke(() =>
                {
                    var clientToRemove = ConnectedClients.FirstOrDefault(c => c.ClientIP == clientIp);
                    if (clientToRemove != null)
                    {
                        ConnectedClients.Remove(clientToRemove);
                        var clientEndPoint = new IPEndPoint(packet.Source.Address, 12001);
                        _screenSender?.RemoveClient(clientEndPoint);
                        UpdateStatusText();
                        Debug.WriteLine($"[Host] Client {clientIp} removed from list.");
                    }
                });
            }
        }

        private async Task ToggleStreamingAsync()
        {
            if (_screenSender != null)
            {
                // --- STOP LOGIC ---
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

                _keyboardManager?.StopSimulation();
                _keyboardManager?.Dispose();
                _keyboardManager = null;

                _vigemManager?.StopSimulation();
                _vigemManager?.Dispose();
                _vigemManager = null;

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

                StreamButtonContent = "Bắt đầu Host";
                PreviewImage = null;
                StatusText = "Đã dừng. 0 client(s).";
                _cancellationTokenSource = null;

                PendingClients.Clear();
                ConnectedClients.Clear();
            }
            else
            {
                // --- START LOGIC ---
                try
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    const int SERVER_PORT = 12000;

                    _sharedUdpPeer = new UdpPeer(SERVER_PORT);
                    _sharedUdpPeer.OnPacketReceived += HandleControlPacket;

                    _ = Task.Run(() => _sharedUdpPeer.StartReceivingAsync(), _cancellationTokenSource.Token);

                    _screenProcessor = ScreenProcessor.Instance;
                    _screenProcessor.Start();

                    _screenSender = new ScreenSender(_sharedUdpPeer, _screenProcessor);
                    _screenSender.OnFrameCaptured += HandleFrameCaptured;

                    _audioManager = new AudioManager(_sharedUdpPeer, AudioConfig.CreateDefault(), isClientMode: false);
                    _audioManager.StartAudioStreaming(AudioInputType.Microphone);

                    _keyboardManager = new KeyboardManager(_sharedUdpPeer, isClientMode: true);
                    _keyboardManager.StartSimulation();

                    _vigemManager = new ViGEmManager(_sharedUdpPeer, isClientMode: false);
                    _vigemManager.StartSimulation();

                    _ = Task.Run(() => _screenSender.SendScreenLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                    _ = Task.Run(() => _networkService.StartTcpListenerLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Lỗi nghiêm trọng khi lắng nghe TCP: {t.Exception?.InnerExceptions.FirstOrDefault()?.Message}", "Lỗi Mạng", MessageBoxButton.OK, MessageBoxImage.Error);
                                _ = ToggleStreamingAsync();
                            });
                        }
                    }, TaskScheduler.Default);

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
            if (_screenSender == null || _cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                const int CLIENT_PORT = 12001;
                var clientAddress = IPAddress.Parse(clientIp);
                var clientEndPoint = new IPEndPoint(clientAddress, CLIENT_PORT);

                _screenSender.AddClient(clientEndPoint);
                _audioManager?.SetTargetEndPoint(clientEndPoint);

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

        private void UpdateStatusText()
        {
            StatusText = $"Đang stream... ({ConnectedClients.Count} client(s) connected)";
        }

        private void HandleFrameCaptured(Image frame)
        {
            var bitmapSource = ToBitmapSource(frame);
            if (bitmapSource == null) return;

            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                PreviewImage = bitmapSource;
            }));

            frame.Dispose();
        }

        // Sửa CS8603: Thêm ? vào BitmapSource để cho phép trả về null
        public static BitmapSource? ToBitmapSource(Image image)
        {
            if (image == null) return null;

            Bitmap? bitmap = image as Bitmap;
            if (bitmap == null)
            {
                Debug.WriteLine("Lỗi ToBitmapSource: Ảnh nhận được không phải là Bitmap.");
                return null;
            }

            System.Drawing.Imaging.BitmapData? bitmapData = null;
            try
            {
                bitmapData = bitmap.LockBits(
                  new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                  System.Drawing.Imaging.ImageLockMode.ReadOnly,
                  bitmap.PixelFormat);

                var pixelFormat = System.Windows.Media.PixelFormats.Bgr32;

                var bitmapSource = BitmapSource.Create(
                  bitmapData.Width, bitmapData.Height,
                  bitmap.HorizontalResolution, bitmap.VerticalResolution,
                  pixelFormat,
                  null,
                  bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi ToBitmapSource: {ex.Message}");
                return null;
            }
            finally
            {
                if (bitmapData != null)
                {
                    bitmap?.UnlockBits(bitmapData);
                }
            }
        }

        private async Task KickClientAsync(ConnectedClientViewModel? client)
        {
            if (client == null) return;

            var result = MessageBox.Show($"Bạn có chắc muốn đuổi (kick) client: {client.DisplayName}?",
                                         "Xác nhận Kick",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.No) return;

            try
            {
                var clientIp = IPAddress.Parse(client.ClientIP);
                var clientEndPoint = new IPEndPoint(clientIp, 12001);

                _screenSender?.RemoveClient(clientEndPoint);

                var kickPacket = new UdpPacket(UdpPacketType.Kick, 0);
                if (_sharedUdpPeer != null)
                {
                    await _sharedUdpPeer.SendToAsync(kickPacket, clientEndPoint);
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    ConnectedClients.Remove(client);
                    UpdateStatusText();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Lỗi khi Kick: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            if (_screenSender != null)
            {
                ToggleStreamingAsync().Wait();
            }
        }
    }
}