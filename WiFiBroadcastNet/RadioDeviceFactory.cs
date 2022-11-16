using System.Net.NetworkInformation;
using SharpPcap.LibPcap;
using WiFiBroadcastNet.SystemHelpers;

namespace WiFiBroadcastNet;

public class RadioDeviceFactory
{
    private readonly IOsCommandHelper _currentOsHelper;

    public RadioDeviceFactory()
    {
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            _currentOsHelper = new LinuxHelpers();
        }
        else
        {
            _currentOsHelper = new NotImplementedHelpers();
        }
    }

    public Device CreateDeviceByName(string deviceName)
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        var networkInterface = interfaces.Single(iface =>
            string.Equals(iface.Name, deviceName, StringComparison.InvariantCultureIgnoreCase));
        var physicalAddress = networkInterface.GetPhysicalAddress();
        var pcapInterface =
            LibPcapLiveDeviceList.Instance.Single(device => device.MacAddress?.Equals(physicalAddress) ?? false);
        return new Device(pcapInterface, networkInterface, _currentOsHelper);
    }
}