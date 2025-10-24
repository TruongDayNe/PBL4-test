using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System;
using System.Windows;
using System.Windows.Controls; // Thêm using này
using System.Windows.Navigation;
using WPFUI_NEW.Views;

namespace WPFUI_NEW
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Điều hướng đến trang LỰA CHỌN mặc định khi khởi động
            MainFrame.Navigate(new SelectionPage());
        }

        // Xử lý sự kiện khi Frame điều hướng xong để cập nhật tiêu đề
        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Cập nhật tiêu đề cửa sổ dựa trên tiêu đề của Page
            if (e.Content is Page page && !string.IsNullOrEmpty(page.Title))
            {
                // Cố gắng đặt tiêu đề cụ thể hơn nếu có
                if (page is HostView)
                {
                    this.Title = "My Stream App - Host Mode";
                }
                else if (page is ClientView)
                {
                    this.Title = "My Stream App - Client Mode";
                }
                else if (page is SelectionPage)
                {
                    this.Title = "My Stream App - Select Mode";
                }
                else
                {
                    this.Title = $"My Stream App - {page.Title}"; // Fallback
                }
            }
            else
            {
                this.Title = "My Stream App"; // Tiêu đề mặc định
            }

            // Xóa lịch sử điều hướng để người dùng không thể nhấn Back từ Host/Client về Selection
            if (MainFrame.CanGoBack && !(e.Content is SelectionPage)) // Chỉ xóa khi không phải đang ở trang chọn
            {
                // Xóa mục trước đó (SelectionPage) khỏi lịch sử
                MainFrame.RemoveBackEntry();
            }
        }
    }
}

