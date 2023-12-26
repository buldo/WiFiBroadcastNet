using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Osd.Wpf.ViewModels;

public class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        Osd = new();
        ReceiverControl = new();
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