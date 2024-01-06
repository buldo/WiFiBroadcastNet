using Android.Hardware.Usb;

using Microsoft.Extensions.Logging;
using Rtl8812auNet;
using WiFiBroadcastNet.Devices;

namespace ReceiverApp.Platforms.Android.BackgroundService;

public class AndroidDevicesProvider : IDevicesProvider
{
    private readonly WiFiDriver _wiFiDriver;
    private readonly UsbDevice _device;
    private readonly UsbDeviceConnection _connection;
    private readonly ILoggerFactory _loggerFactory;

    public AndroidDevicesProvider(
        WiFiDriver wiFiDriver,
        UsbDevice device,
        UsbDeviceConnection connection,
        ILoggerFactory loggerFactory)
    {
        _wiFiDriver = wiFiDriver;
        _device = device;
        _connection = connection;
        _loggerFactory = loggerFactory;
    }

    public List<IRadioDevice> GetDevices()
    {
        List<IRadioDevice> devices = new();

        var adapter = new RtlUsbDevice(
            _device,
            _connection,
            _loggerFactory.CreateLogger<RtlUsbDevice>());
        var d1 = _wiFiDriver.CreateRtlDevice(adapter);
        var wrapper = new UserspaceRadioDevice(d1);
        devices.Add(wrapper);

        return devices;
    }
}