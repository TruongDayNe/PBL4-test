using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input; // Cần cho ICommand
using WPFUI_NEW.ViewModels; // Đảm bảo using này đúng

namespace WPFUI_NEW.ViewModels
{
    // *** QUAN TRỌNG: Phải có 'partial' ***
    public partial class MainViewModel : ObservableObject
    {
        // === ViewModel con ===
        public HostViewModel HostViewModel { get; }
        public ClientViewModel ClientViewModel { get; }
        public SelectionViewModel SelectionViewModel { get; }
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
        public IRelayCommand<string> NavigateToClientStreamCommand { get; }

        // === Constructor ===
        public MainViewModel()
        {
            // Khởi tạo command điều hướng nội bộ
            NavigateToClientStreamCommand = new RelayCommand<string>(NavigateToClientStream);

            // Khởi tạo các ViewModel con, truyền command/action
            HostViewModel = new HostViewModel();
            ClientViewModel = new ClientViewModel();
            // --- SỬA ĐỔI BẮT ĐẦU ---
            // Tạo một command mới kết hợp cả BẮT ĐẦU STREAM và ĐIỀU HƯỚNG
            
            ClientConnectViewModel = new ClientConnectViewModel(NavigateToClientStream);

            // SỬA ĐỔI: Điều hướng thẳng đến HostViewModel
            ShowHostViewCommand = new RelayCommand(ShowHostView);
            ShowClientViewCommand = new RelayCommand(ShowClientConnectView);
            ShowSelectionViewCommand = new RelayCommand(ShowSelectionView);

            SelectionViewModel = new SelectionViewModel(ShowHostViewCommand, ShowClientViewCommand);

            CurrentViewModel = SelectionViewModel;
        }

        // === Hàm điều hướng ===
        private void ShowHostView() => CurrentViewModel = HostViewModel;
        private void ShowClientConnectView() => CurrentViewModel = ClientConnectViewModel;
        private void ShowSelectionView() => CurrentViewModel = SelectionViewModel;

        private void NavigateToClientStream(string hostIp)
        {
            // 1. Gán IP cho ClientViewModel
            if (!string.IsNullOrEmpty(hostIp))
            {
                ClientViewModel.HostIpAddress = hostIp;
            }

            // 2. Chuyển sang màn hình ClientView
            CurrentViewModel = ClientViewModel;

            // 3. TỰ ĐỘNG KÍCH HOẠT KẾT NỐI
            if (ClientViewModel.ConnectCommand.CanExecute(null))
            {
                // Chạy và quên đi, ToggleConnectionAsync sẽ tự xử lý
                _ = ClientViewModel.ConnectCommand.ExecuteAsync(null);
            }
        }


    
        // === Hàm Cleanup ===
        public void Cleanup()
        {
            (HostViewModel as HostViewModel)?.Cleanup();
            (ClientViewModel as ClientViewModel)?.Cleanup();
        }
    }
}