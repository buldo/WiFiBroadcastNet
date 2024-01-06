using Android.Content;
using Android.Hardware.Usb;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Views;

using LibUsbDotNet;

using ReceiverApp.Platforms.Android;

using Context = Android.Content.Context;

namespace ReceiverApp.Platforms.Android.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly Context _context;
    private readonly MyBroadcastReceiver _broadcastReceiver;
    private readonly UsbManager _usbManager;

    private string _statusText;
    private bool _isStarted;
    private UsbDevice? _selectedDevice;

    public MainViewModel(Context context)
    {
        _context = context;
        _usbManager = (UsbManager)_context.GetSystemService(Context.UsbService)!;
        _statusText = "Init";

        _broadcastReceiver = new MyBroadcastReceiver(this);
        var filter = new IntentFilter(IntentActions.UsbPermission);
        context.RegisterReceiver(_broadcastReceiver, filter);

        StartCommand = new RelayCommand(ExecuteStart, CanExecuteStart);

        AndroidServiceManager.StartWfbService();
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsStarted
    {
        get => _isStarted;
        private set
        {
            if (SetProperty(ref _isStarted, value))
            {
                StartCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public RelayCommand StartCommand { get; }

    private bool CanExecuteStart()
    {
        return !IsStarted;
    }

    private void ExecuteStart()
    {
        IsStarted = true;
        StatusText = "Starting";

        if (_usbManager.DeviceList == null)
        {
            StatusText = "Device list empty";
            return;
        }

        var (_, device) = _usbManager.DeviceList.FirstOrDefault(pair => pair.Value.ManufacturerName == "Realtek");
        if (device != null)
        {
            _selectedDevice = device;
            if (_usbManager.HasPermission(device))
            {
                StatusText =
                    $"Device found:{device.DeviceName}" +
                    System.Environment.NewLine +
                    "Starting...";
                StartReceiving();
            }
            else
            {
                StatusText =
                    $"Device found:{device.DeviceName}" +
                    System.Environment.NewLine +
                    "Requesting permissions...";

                var pi = PendingIntent.GetBroadcast(
                    _context,
                    0,
                    new Intent(IntentActions.UsbPermission),
                    PendingIntentFlags.Immutable);
                _usbManager.RequestPermission(device, pi);
            }
        }
        else
        {
            StatusText = "No RTL8812AU device found";
        }
    }

    private void StartReceiving()
    {
        if (_selectedDevice == null)
        {
            StatusText = "No selected device";
            return;
        }

        StatusText =
            $"Device found:{_selectedDevice.DeviceName}" +
            System.Environment.NewLine +
            "Starting...";

        var connection = _usbManager.OpenDevice(_selectedDevice);
        if (connection == null)
        {
            StatusText = "Not able to get usb connection";
            return;
        }

        if (AndroidServiceManager.Service == null)
        {
            StatusText = "Foreground service not found";
            return;
        }

        AndroidServiceManager.Service.StartRx(_selectedDevice, connection);
    }

    private sealed class MyBroadcastReceiver : BroadcastReceiver
    {
        private readonly MainViewModel _parent;

        public MyBroadcastReceiver(MainViewModel parent)
        {
            _parent = parent;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == IntentActions.UsbPermission)
            {
                _parent.StartReceiving();
            }
        }
    }
}
