using CommonAbstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommonViewModels;

public class ReceiverControlViewModel : ObservableObject
{
    private readonly IWfbHost _wfbHost;
    private int _devicesCount;
    private bool _isStarted;

    public ReceiverControlViewModel(IWfbHost host)
    {
        _wfbHost = host;
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
        DevicesCount = _wfbHost.GetDevicesCount();
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
        _wfbHost.Start(ChannelsSelector.SelectedChannel);
        OnStarted();
    }

    public void Stop()
    {
        _wfbHost.Stop();
    }
}