using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;

namespace WPFUI_NEW
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Tạo console window để xem debug logs
            AllocConsole();
            Console.WriteLine("========================================");
            Console.WriteLine("       PBL4 Remote Desktop Console      ");
            Console.WriteLine("========================================\n");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            FreeConsole();
            base.OnExit(e);
        }
    }

}
