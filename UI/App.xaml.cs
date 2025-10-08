using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider? ServiceProvider { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        // Register services
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(
            sp => LoggerFactory.Create(builder => builder.AddConsole()));
        ServiceProvider = services.BuildServiceProvider();
        base.OnStartup(e);
    }
}

