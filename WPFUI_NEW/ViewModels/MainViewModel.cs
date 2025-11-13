using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input; // Cần cho ICommand
using WPFUI_NEW.ViewModels; // Đảm bảo using này đúng
using RealTimeUdpStream.Core.Util; // For ConfigHelper

namespace WPFUI_NEW.ViewModels
{
    // *** QUAN TRỌNG: Phải có 'partial' ***
    public partial class MainViewModel : ObservableObject
    {
        // === ViewModel con ===
        public HostViewModel HostViewModel { get; }
        public ClientViewModel ClientViewModel { get; }
        public SelectionViewModel SelectionViewModel { get; }
        public HostPreStreamViewModel HostPreStreamViewModel { get; }
        public ClientConnectViewModel ClientConnectViewModel { get; }

        // === ViewModel hiện tại (Chỉ dùng [ObservableProperty]) ===
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSelectionViewActive))]
        private ObservableObject _currentViewModel; // Chỉ có field backing '_', không có property public thủ công

        // === Thuộc tính trạng thái ===
        public bool IsSelectionViewActive => CurrentViewModel == SelectionViewModel; // Chỉ có định nghĩa này

        // === Commands ===
        public IRelayCommand ShowHostViewCommand { get; }
        public IRelayCommand ShowClientViewCommand { get; }
        public IRelayCommand ShowSelectionViewCommand { get; }
        public IRelayCommand NavigateToClientStreamCommand { get; }

        // === Constructor ===
        public MainViewModel()
        {
            // Load key mapping config khi khởi động app
            try
            {
                ConfigHelper.LoadConfig();
                System.Diagnostics.Debug.WriteLine("✓ Config loaded on app startup");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Failed to load config: {ex.Message}");
                // Continue anyway with default config
            }

            // Khởi tạo command điều hướng nội bộ
            NavigateToClientStreamCommand = new RelayCommand(NavigateToClientStream);

            // Khởi tạo các ViewModel con, truyền command/action
            HostViewModel = new HostViewModel();
            ClientViewModel = new ClientViewModel();
            // --- SỬA ĐỔI BẮT ĐẦU ---
            // Tạo một command mới kết hợp cả BẮT ĐẦU STREAM và ĐIỀU HƯỚNG
            var startAndNavigateCommand = new AsyncRelayCommand(async () =>
            {
                // 1. Điều hướng (chuyển view) TRƯỚC TIÊN
                // Bằng cách này, người dùng sẽ thấy màn hình HostView ngay lập tức.
                NavigateToHostStream();

                // 2. Thử bắt đầu stream
                try
                {
                    if (HostViewModel.StartStreamCommand.CanExecute(null))
                    {
                        // Thực thi logic stream.
                        // Nếu có lỗi, HostViewModel sẽ tự hiển thị MessageBox.
                        await HostViewModel.StartStreamCommand.ExecuteAsync(null);
                    }
                }
                catch (Exception ex)
                {
                    // Lỗi đã được HostViewModel xử lý (hiển thị),
                    // chúng ta chỉ cần ghi log ở đây để gỡ lỗi nếu muốn.
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] Lỗi khi thực thi lệnh stream: {ex.Message}");
                    // Quan trọng: Không ném lại lỗi (re-throw) để ứng dụng không bị crash
                    // và chúng ta vẫn ở lại màn hình HostView.
                }
            });


            // Truyền command kết hợp mới này vào HostPreStreamViewModel
            HostPreStreamViewModel = new HostPreStreamViewModel(startAndNavigateCommand);
            // --- SỬA ĐỔI KẾT THÚC ---

            ClientConnectViewModel = new ClientConnectViewModel(NavigateToClientStream);

            // Khởi tạo command điều hướng chính
            ShowHostViewCommand = new RelayCommand(ShowHostPreStreamView);
            ShowClientViewCommand = new RelayCommand(ShowClientConnectView);
            ShowSelectionViewCommand = new RelayCommand(ShowSelectionView);

            // Khởi tạo SelectionViewModel
            SelectionViewModel = new SelectionViewModel(ShowHostViewCommand, ShowClientViewCommand);

            // Đặt ViewModel ban đầu (Source generator sẽ tạo 'CurrentViewModel' public)
            CurrentViewModel = SelectionViewModel;
        }

        // === Hàm điều hướng ===
        private void ShowHostPreStreamView() => CurrentViewModel = HostPreStreamViewModel;
        private void ShowClientConnectView() => CurrentViewModel = ClientConnectViewModel;
        private void ShowSelectionView() => CurrentViewModel = SelectionViewModel;
        private void NavigateToHostStream() => CurrentViewModel = HostViewModel;
        private void NavigateToClientStream() => CurrentViewModel = ClientViewModel;

        // === Hàm Cleanup ===
        public void Cleanup()
        {
            (HostViewModel as HostViewModel)?.Cleanup();
            (ClientViewModel as ClientViewModel)?.Cleanup();
        }
    }
}