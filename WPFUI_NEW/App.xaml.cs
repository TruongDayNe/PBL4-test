using System.Configuration;
using System.Data;
using System.Windows;

namespace WPFUI_NEW
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Show console window for debugging
            AllocConsole();
        }
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
    }

}
