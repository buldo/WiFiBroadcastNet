using Rtl8812auNet;

namespace WiFiBroadcastNet.Devices;

public class AutoDevicesProvider : IDevicesProvider
{
    private readonly WiFiDriver _wiFiDriver;

    public AutoDevicesProvider(
        WiFiDriver wiFiDriver)
    {
        _wiFiDriver = wiFiDriver;
    }

    public List<IRadioDevice> GetDevices()
    {
        var usbDevices = _wiFiDriver.GetUsbDevices();
        List<IRadioDevice> devices = new();
        foreach (var device in usbDevices)
        {
            var rtlDevice = _wiFiDriver.CreateRtlDevice(device);
            var wrapper = new UserspaceRadioDevice(rtlDevice);
            devices.Add(wrapper);
        }

        return devices;
    }
}