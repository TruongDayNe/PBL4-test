using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation; // Thêm using này

namespace WPFUI_NEW.Views
{
    public partial class SelectionPage : Page
    {
        public SelectionPage()
        {
            InitializeComponent();
        }

        private void HostButton_Click(object sender, RoutedEventArgs e)
        {
            // Lấy NavigationService của Frame chứa trang này và điều hướng
            NavigationService?.Navigate(new HostView());
        }

        private void ClientButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new ClientView());
        }
    }
}


