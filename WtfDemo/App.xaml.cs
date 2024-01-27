using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WtfDemo;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Gets the current <see cref="App"/> instance in use
    /// </summary>
    public new static App Current => (App)Application.Current;

    public IServiceProvider Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        Services = ConfigureServices();
        base.OnStartup(e);
    }

    private IServiceProvider ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder =>
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddFilter("Rtl8812auNet.*", LogLevel.Warning)
                .AddDebug()
        );

        return serviceCollection.BuildServiceProvider();
    }
}

