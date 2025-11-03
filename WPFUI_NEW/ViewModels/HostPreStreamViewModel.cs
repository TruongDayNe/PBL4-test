using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows; // Cần cho Clipboard

namespace WPFUI_NEW.ViewModels
{
    // Lớp cơ sở cho các ViewModel con trong danh sách
    public partial class ClientListItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _clientId;
        [ObservableProperty] private string _clientIP;
        [ObservableProperty] private string _displayName;
    }

    // ViewModel cho danh sách "Chờ duyệt"
    public partial class ClientRequestViewModel : ClientListItemViewModel { }

    // ViewModel cho danh sách "Đã kết nối"
    public partial class ConnectedClientViewModel : ClientListItemViewModel
    {
        [ObservableProperty] private int _ping;
        [ObservableProperty] private double _packetLoss;
    }

    // ViewModel chính cho HostPreStreamView
    public partial class HostPreStreamViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _hostIpAddress = "192.168.1.100"; // TODO: Lấy IP thật

        public ObservableCollection<ClientRequestViewModel> PendingClients { get; }
        public ObservableCollection<ConnectedClientViewModel> ConnectedClients { get; }

        // 1. SỬA ĐỔI: Chuyển đây thành một thuộc tính public
        public IAsyncRelayCommand StartStreamCommand { get; }

        // 2. SỬA ĐỔI: Constructor (Hàm khởi tạo)
        // Bây giờ nó nhận một lệnh điều hướng từ MainViewModel
        public HostPreStreamViewModel(IAsyncRelayCommand navigateAndStartStreamCommand) // SỬA ĐỔI: Đổi tên và kiểu
        {
            // Gán lệnh được truyền vào cho nút "BẮT ĐẦU STREAM"
            StartStreamCommand = navigateAndStartStreamCommand;

            // --- Dữ liệu giả để thiết kế (Xóa khi làm thật) ---
            PendingClients = new ObservableCollection<ClientRequestViewModel>
            {
                new ClientRequestViewModel { ClientId = "1", ClientIP = "192.168.1.150", DisplayName = "Client A (192.168.1.150)" }
            };
            ConnectedClients = new ObservableCollection<ConnectedClientViewModel>
            {
                new ConnectedClientViewModel { ClientId = "2", ClientIP = "192.168.1.200", DisplayName = "Client B (Vip)", Ping = 15, PacketLoss = 0.1 }
            };
            // --- Kết thúc dữ liệu giả ---
        }

        [RelayCommand]
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

        [RelayCommand]
        private void AcceptClient(ClientRequestViewModel client)
        {
            if (client == null) return;
            PendingClients.Remove(client);
            var connectedClient = new ConnectedClientViewModel
            {
                ClientId = client.ClientId,
                ClientIP = client.ClientIP,
                DisplayName = client.DisplayName,
            };
            ConnectedClients.Add(connectedClient);
            // TODO: Gửi tín hiệu "Chấp nhận" cho client
        }

        [RelayCommand]
        private void RejectClient(ClientRequestViewModel client)
        {
            if (client == null) return;
            PendingClients.Remove(client);
            // TODO: Gửi tín hiệu "Từ chối" cho client
        }

        [RelayCommand]
        private void KickClient(ConnectedClientViewModel client)
        {
            if (client == null) return;
            ConnectedClients.Remove(client);
            // TODO: Gửi tín hiệu "Kick" cho client
        }
    }
}