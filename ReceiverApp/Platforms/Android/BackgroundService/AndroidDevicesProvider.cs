using Microsoft.Extensions.Logging;
using Rtl8812auNet;
using WiFiBroadcastNet.Devices;

namespace ReceiverApp.Platforms.Android.BackgroundService;

public class AndroidDevicesProvider : IDevicesProvider
{
    private readonly WiFiDriver _wiFiDriver;
    private readonly ILoggerFactory _loggerFactory;

    public AndroidDevicesProvider(
        WiFiDriver wiFiDriver,
        ILoggerFactory loggerFactory)
    {
        _wiFiDriver = wiFiDriver;
        _loggerFactory = loggerFactory;
    }

    public List<IRadioDevice> GetDevices()
    {
        var device = AndroidServiceManager.Device;
        var connection = AndroidServiceManager.Connection;


        List<IRadioDevice> devices = new();

        var adapter = new RtlUsbDevice(device, connection, _loggerFactory.CreateLogger<RtlUsbDevice>());
        var d1 = _wiFiDriver.CreateRtlDevice(adapter);
        var wrapper = new UserspaceRadioDevice(d1);
        devices.Add(wrapper);

        return devices;
    }
}