using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace WPFUI_NEW.ViewModels
{
    /// <summary>
    /// ViewModel cho màn hình lựa chọn Host/Client ban đầu.
    /// Nó nhận các command từ MainViewModel để thực hiện điều hướng.
    /// </summary>
    public partial class SelectionViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        // Các Command này sẽ được "truyền" vào từ MainViewModel
        public ICommand ShowHostViewCommand { get; }
        public ICommand ShowClientViewCommand { get; }

        public SelectionViewModel(ICommand showHost, ICommand showClient)
        {
            ShowHostViewCommand = showHost;
            ShowClientViewCommand = showClient;
        }

        // Constructor rỗng cho designer XAML
        public SelectionViewModel() { }
    }
}