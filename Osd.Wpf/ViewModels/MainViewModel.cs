using CommonViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Osd.Wpf.Services;
using Rtl8812auNet;

namespace Osd.Wpf.ViewModels;

public class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        Osd = new();

        var loggerFactory = App.Current.Services.GetRequiredService<ILoggerFactory>();
        var driver = new WiFiDriver(loggerFactory);
        var host = new WfbHost(driver, loggerFactory);
        ReceiverControl = new(host);
        ReceiverControl.Started += ReceiverControlOnStarted;
    }

    public OsdVideoViewModel Osd { get; }

    public ReceiverControlViewModel ReceiverControl { get; }

    private void ReceiverControlOnStarted(object? sender, EventArgs e)
    {
        Osd.StartPlay();
    }

    public void Stop()
    {
        ReceiverControl.Stop();
    }
}
