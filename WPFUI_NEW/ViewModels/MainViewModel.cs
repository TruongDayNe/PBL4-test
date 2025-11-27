using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input; // Cần cho ICommand
using WPFUI_NEW.ViewModels; // Đảm bảo using này đúng
using RealTimeUdpStream.Core.Util; // For ConfigHelper

namespace WPFUI_NEW.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public HostViewModel HostViewModel { get; }
        public ClientViewModel ClientViewModel { get; }
        public SelectionViewModel SelectionViewModel { get; }
        public ClientConnectViewModel ClientConnectViewModel { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSelectionViewActive))]
        [NotifyPropertyChangedFor(nameof(IsClientViewActive))]
        private ObservableObject _currentViewModel;

        public bool IsSelectionViewActive => CurrentViewModel == SelectionViewModel;
        // Thuộc tính quan trọng để chuyển chế độ màn hình
        public bool IsClientViewActive => CurrentViewModel == ClientViewModel;

        public IRelayCommand ShowHostViewCommand { get; }
        public IRelayCommand ShowClientViewCommand { get; }
        public IRelayCommand ShowSelectionViewCommand { get; }
        public IRelayCommand<string> NavigateToClientStreamCommand { get; }

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

            HostViewModel = new HostViewModel();
            ClientViewModel = new ClientViewModel();
            ClientConnectViewModel = new ClientConnectViewModel(NavigateToClientStream);

            ShowHostViewCommand = new RelayCommand(ShowHostView);
            ShowClientViewCommand = new RelayCommand(ShowClientConnectView);
            ShowSelectionViewCommand = new RelayCommand(ShowSelectionView);

            SelectionViewModel = new SelectionViewModel(ShowHostViewCommand, ShowClientViewCommand);

            CurrentViewModel = SelectionViewModel;
        }

        private void ShowHostView()
        {
            CurrentViewModel = HostViewModel;

            // Chỉ khi chuyển sang màn hình Host mới bắt đầu lắng nghe
            HostViewModel.StartTcpListening();
        }
        private void ShowClientConnectView() => CurrentViewModel = ClientConnectViewModel;
        private void ShowSelectionView() => CurrentViewModel = SelectionViewModel;

        private void NavigateToClientStream(string hostIp)
        {
            if (!string.IsNullOrEmpty(hostIp))
            {
                ClientViewModel.HostIpAddress = hostIp;
            }

            CurrentViewModel = ClientViewModel;

            if (ClientViewModel.ConnectCommand.CanExecute(null))
            {
                _ = ClientViewModel.ConnectCommand.ExecuteAsync(null);
            }
        }

        public void Cleanup()
        {
            (HostViewModel as HostViewModel)?.Cleanup();
            (ClientViewModel as ClientViewModel)?.Cleanup();
        }
    }
}