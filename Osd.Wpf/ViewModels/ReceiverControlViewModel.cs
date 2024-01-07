using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Osd.Wpf.Services;
using Rtl8812auNet;

namespace Osd.Wpf.ViewModels;

public class ReceiverControlViewModel : ObservableObject
{
    private readonly WiFiDriver _wifiDriver;
    private WfbHost _wfbHost;
    private int _devicesCount;
    private bool _isStarted;
    private readonly ILoggerFactory _loggerFactory;

    public ReceiverControlViewModel()
    {
        _loggerFactory = App.Current.Services.GetRequiredService<ILoggerFactory>();
        _wifiDriver = new WiFiDriver(_loggerFactory);
        RefreshDevicesCommand = new RelayCommand(ExecuteRefreshDevices, CanExecuteRefreshDevices);
        ChangeChannelCommand = new RelayCommand(ExecuteChangeChannel, CanExecuteChangeChannel);
        StartCommand = new RelayCommand(ExecuteStart, CanExecuteStart);
        UpdateDevicesList();
    }

    public event EventHandler<EventArgs> Started;

    public RelayCommand RefreshDevicesCommand { get; }

    public RelayCommand ChangeChannelCommand { get; }

    public RelayCommand StartCommand { get; }

    public ChannelsListViewModel ChannelsSelector { get; } = new();

    public bool IsStarted
    {
        get => _isStarted;
        private set
        {
            if (SetProperty(ref _isStarted, value))
            {
                RefreshDevicesCommand.NotifyCanExecuteChanged();
                StartCommand.NotifyCanExecuteChanged();
                ChangeChannelCommand.NotifyCanExecuteChanged();
                OnStarted();
            }
        }
    }

    public int DevicesCount
    {
        get => _devicesCount;
        private set
        {
            if (SetProperty(ref _devicesCount, value))
            {
                RefreshDevicesCommand.NotifyCanExecuteChanged();
                StartCommand.NotifyCanExecuteChanged();
                ChangeChannelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void UpdateDevicesList()
    {
        DevicesCount = _wifiDriver.GetUsbDevices().Count;
    }

    private void ExecuteRefreshDevices()
    {
        UpdateDevicesList();
    }

    private bool CanExecuteRefreshDevices()
    {
        return !IsStarted;
    }

    private bool CanExecuteChangeChannel()
    {
        return IsStarted;
    }

    private void ExecuteChangeChannel()
    {
        _wfbHost.SetChannel(ChannelsSelector.SelectedChannel);
    }

    protected virtual void OnStarted()
    {
        Started?.Invoke(this, EventArgs.Empty);
    }

    private bool CanExecuteStart()
    {
        return !IsStarted && DevicesCount > 0;
    }

    private void ExecuteStart()
    {
        IsStarted = true;
        _wfbHost = new WfbHost(_wifiDriver, _loggerFactory);
        _wfbHost.Start(ChannelsSelector.SelectedChannel);
        OnStarted();
    }

    public void Stop()
    {
        _wifiDriver.Dispose();
    }
}