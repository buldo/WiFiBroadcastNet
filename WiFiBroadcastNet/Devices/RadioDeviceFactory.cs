using System.Net.NetworkInformation;
using SharpPcap.LibPcap;
using WiFiBroadcastNet.SystemHelpers;

namespace WiFiBroadcastNet.Devices;

public class RadioDeviceFactory
{
    private readonly LinuxHelpers _currentOsHelper;

    public RadioDeviceFactory()
    {
        _currentOsHelper = new LinuxHelpers();
    }

    public List<DeviceDescription> GetWifiAdapters()
    {
        var allDevices = _currentOsHelper.GetWirelessInterfaces();

        var devices = new List<DeviceDescription>();
        foreach (var dev in allDevices)
        {
            var deviceFileName = $"/sys/class/net/{dev.IfName}/device/uevent";
            if (!File.Exists(deviceFileName))
            {
                continue;
            }

            var ueventContent = File.ReadAllLines(deviceFileName);
            var ueventDesc = UeventDescription.Parse(ueventContent);

            var deviceDescription = new DeviceDescription(dev, ueventDesc);

            devices.Add(deviceDescription);
        }

        return devices;
    }

    public PcapRadioDevice CreateDeviceByName(string deviceName)
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        var networkInterface = interfaces.Single(iface =>
            string.Equals(iface.Name, deviceName, StringComparison.InvariantCultureIgnoreCase));
        var physicalAddress = networkInterface.GetPhysicalAddress();
        var pcapInterface =
            LibPcapLiveDeviceList.Instance.Single(device => device.MacAddress?.Equals(physicalAddress) ?? false);
        return new PcapRadioDevice(pcapInterface, networkInterface, _currentOsHelper);
    }
}