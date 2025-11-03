using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows; // Cần cho Application.Current.Dispatcher

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
        private readonly Action _onHostAcceptedCallback;

        // 2. SỬA ĐỔI: Constructor (Hàm khởi tạo)
        // Nhận một hành động (Action) từ MainViewModel
        public ClientConnectViewModel(Action onHostAcceptedCallback)
        {
            _onHostAcceptedCallback = onHostAcceptedCallback;
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
                // --- GIẢ LẬP MẠNG (Xóa khi làm thật) ---
                await Task.Delay(3000); // Giả lập đang kết nối...

                // TODO: Gọi service mạng để kết nối TCP (handshake)
                bool success = true; // Giả lập kết nối thành công

                if (success)
                {
                    StatusText = "Kết nối thành công! Đang chờ Host chấp nhận...";
                    Debug.WriteLine("[Client] Đã gửi yêu cầu, đang chờ Host...");

                    // --- GIẢ LẬP HOST CHẤP NHẬN (Xóa khi làm thật) ---
                    await Task.Delay(2000);
                    OnHostAccepted(); // Gọi hàm điều hướng
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
        private void OnHostAccepted()
        {
            // Phải chạy trên luồng UI chính
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnecting = false;
                Debug.WriteLine("[Client] Host đã chấp nhận!");

                // GỌI HÀNH ĐỘNG ĐỂ ĐIỀU HƯỚNG
                _onHostAcceptedCallback?.Invoke();
            });
        }

        private void OnHostRejected()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = "Host đã từ chối kết nối của bạn.";
                IsConnecting = false;
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