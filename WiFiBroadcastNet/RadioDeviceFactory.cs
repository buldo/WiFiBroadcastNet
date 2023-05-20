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

    public List<DeviceDescription> GetWifiAdapters()
    {
        var allDevices = NetworkInterface.GetAllNetworkInterfaces();

        var devices = new List<DeviceDescription>();
        foreach (var dev in allDevices)
        {
            var phyIndexFileName = $"/sys/class/net/{dev.Name}/phy80211/index";
            if (!File.Exists(phyIndexFileName))
            {
                continue;
            }

            var phyIndex = int.Parse(File.ReadAllText(phyIndexFileName));
            
            var deviceFileName = $"/sys/class/net/{dev.Name}/device/uevent";
            if (!File.Exists(deviceFileName))
            {
                continue;
            }
            
            var ueventContent = File.ReadAllLines(deviceFileName);
            var ueventDesc = UeventDescription.Parse(ueventContent);

            var deviceDescription = new DeviceDescription
            {
                UeventDescription = ueventDesc,
                Interface = dev,
                PhyIndex = phyIndex
            };
            
            devices.Add(deviceDescription);
        }

        return devices;
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