using System.Configuration;
using System.Data;
using System.Reflection;
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
            
            // Force load required DLLs trước khi dùng System.Text.Json
            ForceLoadDependencies();
        }
        
        private void ForceLoadDependencies()
        {
            try
            {
                Console.WriteLine("[App] Force loading dependencies...");
                
                // Get application directory
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // Force load Microsoft.Bcl.AsyncInterfaces
                string asyncInterfacesPath = System.IO.Path.Combine(appDir, "Microsoft.Bcl.AsyncInterfaces.dll");
                if (System.IO.File.Exists(asyncInterfacesPath))
                {
                    var asyncAssembly = Assembly.LoadFrom(asyncInterfacesPath);
                    Console.WriteLine($"[App] ✓ Loaded: {asyncAssembly.FullName}");
                }
                else
                {
                    Console.WriteLine($"[App] ✗ NOT FOUND: {asyncInterfacesPath}");
                }
                
                // Force load System.Text.Json
                string textJsonPath = System.IO.Path.Combine(appDir, "System.Text.Json.dll");
                if (System.IO.File.Exists(textJsonPath))
                {
                    var jsonAssembly = Assembly.LoadFrom(textJsonPath);
                    Console.WriteLine($"[App] ✓ Loaded: {jsonAssembly.FullName}");
                }
                
                // Force load System.Text.Encodings.Web
                string encodingsPath = System.IO.Path.Combine(appDir, "System.Text.Encodings.Web.dll");
                if (System.IO.File.Exists(encodingsPath))
                {
                    var encodingsAssembly = Assembly.LoadFrom(encodingsPath);
                    Console.WriteLine($"[App] ✓ Loaded: {encodingsAssembly.FullName}");
                }
                
                Console.WriteLine("[App] Dependencies loaded successfully!\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] ❌ Failed to force load dependencies: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            FreeConsole();
            base.OnExit(e);
        }
    }

}
