using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WPFUI_NEW.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // 1. Các ViewModel con
        public HostViewModel HostViewModel { get; }
        public ClientViewModel ClientViewModel { get; }
        public SelectionViewModel SelectionViewModel { get; }

        // 2. Thuộc tính ViewModel hiện tại
        [ObservableProperty]
        // SỬA ĐỔI: Thêm attribute này
        [NotifyPropertyChangedFor(nameof(IsSelectionViewActive))]
        private ObservableObject _currentViewModel;

        // 3. THUỘC TÍNH MỚI ĐỂ SỬA LỖI
        /// <summary>
        /// Trả về True nếu ViewModel hiện tại là SelectionViewModel.
        /// Dùng để ẩn/hiện nút "Back".
        /// </summary>
        public bool IsSelectionViewActive => CurrentViewModel == SelectionViewModel;

        // 4. Các Command điều hướng
        public IRelayCommand ShowHostViewCommand { get; }
        public IRelayCommand ShowClientViewCommand { get; }
        public IRelayCommand ShowSelectionViewCommand { get; }

        public MainViewModel()
        {
            // (Phần khởi tạo command giữ nguyên)
            ShowHostViewCommand = new RelayCommand(ShowHostView);
            ShowClientViewCommand = new RelayCommand(ShowClientView);
            ShowSelectionViewCommand = new RelayCommand(ShowSelectionView);

            // (Phần khởi tạo ViewModel con giữ nguyên)
            HostViewModel = new HostViewModel();
            ClientViewModel = new ClientViewModel();
            SelectionViewModel = new SelectionViewModel(ShowHostViewCommand, ShowClientViewCommand);

            // 5. Đặt màn hình ban đầu
            _currentViewModel = SelectionViewModel;
        }

        private void ShowHostView()
        {
            CurrentViewModel = HostViewModel;
        }

        private void ShowClientView()
        {
            CurrentViewModel = ClientViewModel;
        }

        private void ShowSelectionView()
        {
            CurrentViewModel = SelectionViewModel;
        }

        public void Cleanup()
        {
            // Gọi cleanup trên các VM con
            (HostViewModel as HostViewModel)?.Cleanup();
            (ClientViewModel as ClientViewModel)?.Cleanup();
        }
    }
}