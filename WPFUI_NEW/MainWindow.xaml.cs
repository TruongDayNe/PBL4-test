using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WPFUI_NEW.ViewModels;

namespace WPFUI_NEW
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (this.DataContext is MainViewModel vm)
            {
                vm.Cleanup();
            }
        }

        // Cho phép kéo cửa sổ khi nhấn giữ chuột trái vào thanh tiêu đề
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}