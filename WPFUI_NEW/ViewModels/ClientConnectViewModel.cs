using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Networking; 
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; // Cần cho Application.Current.Dispatcher

namespace WPFUI_NEW.ViewModels
{
    public partial class ClientConnectViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
        private string _hostIpAddress = "127.0.0.1";

        [ObservableProperty]
        private string _statusText = "Đang chờ...";

        [ObservableProperty]
        private bool _isConnecting = false;

        // 1. SỬA ĐỔI: Thêm một trường (field) để lưu trữ hành động điều hướng
        private readonly Action<string> _onHostAcceptedCallback;
        private readonly NetworkService _networkService;

        public ICommand BackToMenuCommand { get; }

        // Sửa Constructor
        public ClientConnectViewModel(Action<string> onHostAcceptedCallback, ICommand backToMenuCommand)
        {
            _onHostAcceptedCallback = onHostAcceptedCallback;
            BackToMenuCommand = backToMenuCommand;
            _networkService = new NetworkService(); // Khởi tạo service
        }

        private bool CanConnect()
        {
            return !string.IsNullOrWhiteSpace(HostIpAddress) && !IsConnecting;
        }

        [RelayCommand(CanExecute = nameof(CanConnect))]
        private async Task ConnectAsync()
        {
            IsConnecting = true;
            StatusText = $"Đang kết nối đến {HostIpAddress}...";
            Debug.WriteLine($"[Client] Bắt đầu kết nối đến {HostIpAddress}");

            try
            {

                // TODO: Gọi service mạng để kết nối TCP (handshake)
                bool success = await _networkService.ConnectToHostAsync(HostIpAddress);

                if (success)
                {
                    StatusText = "Host đã chấp nhận! Đang vào stream...";
                    Debug.WriteLine("[Client] Host đã chấp nhận!");

                    await Task.Delay(1000);
                    OnHostAccepted(HostIpAddress); // Gọi hàm điều hướng
                }
                else
                {
                    StatusText = "Kết nối thất bại. Vui lòng kiểm tra lại IP.";
                    IsConnecting = false;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Lỗi kết nối: {ex.Message}";
                IsConnecting = false;
                Debug.WriteLine($"[Client] Lỗi kết nối: {ex.Message}");
            }
        }

        // 3. SỬA ĐỔI: Hàm này sẽ được gọi khi service mạng báo Host đã chấp nhận
        private void OnHostAccepted(string hostIp)
        {
            // Phải chạy trên luồng UI chính
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnecting = false;
                Debug.WriteLine("[Client] Host đã chấp nhận!");

                // GỌI HÀNH ĐỘNG ĐỂ ĐIỀU HƯỚNG
                _onHostAcceptedCallback?.Invoke(hostIp);
            });
        }

        [RelayCommand]
        private void ShowKeyMapping()
        {
            Debug.WriteLine("[Client] Mở cài đặt phím...");
            // TODO: Mở một cửa sổ/dialog mới cho phép map phím
        }
    }
}