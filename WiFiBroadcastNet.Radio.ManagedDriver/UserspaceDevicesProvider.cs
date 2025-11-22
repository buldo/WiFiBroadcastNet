using Rtl8812auNet;

using WiFiBroadcastNet.Radio.Common;

namespace WiFiBroadcastNet.Radio.ManagedDriver;

public class UserspaceDevicesProvider: IDevicesProvider
{
    private readonly WiFiDriver _wiFiDriver;

    public UserspaceDevicesProvider(WiFiDriver wiFiDriver)
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