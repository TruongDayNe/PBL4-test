using System.ComponentModel;
using System.Windows;
using WPFUI_NEW.ViewModels; // Thêm using

namespace WPFUI_NEW
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Thêm hàm này
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Lấy MainViewModel từ DataContext
            if (this.DataContext is MainViewModel vm)
            {
                // Gọi hàm dọn dẹp
                vm.Cleanup();
            }
        }
    }
}