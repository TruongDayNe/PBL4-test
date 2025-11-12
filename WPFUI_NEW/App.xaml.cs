using Downloaders.Enums;
using FFMpegCore;
using System.Configuration;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using WPFUI_NEW.Downloaders;

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

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Cấu hình thư mục chứa file binary FFmpeg
            // Bạn có thể đặt ở bất kỳ đâu, ví dụ: thư mục gốc của ứng dụng
            var binaryFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FFBinaries");
            if (!Directory.Exists(binaryFolder))
            {
                Directory.CreateDirectory(binaryFolder);
            }

            // Cấu hình GlobalFFOptions để FFMpegCore biết tìm file ở đâu
            //
            GlobalFFOptions.Configure(options =>
            {
                options.BinaryFolder = binaryFolder;
            });

            // Tải về các file binary nếu chúng chưa tồn tại
            //
            await FFMpegDownloader.DownloadBinaries(
                binaries: FFMpegBinaries.FFMpeg | FFMpegBinaries.FFProbe,
                options: GlobalFFOptions.Current
            );
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
