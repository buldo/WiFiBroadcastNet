using System.Diagnostics;

using Bld.Libnl.Net.Netlink;
using Bld.Libnl.Net.Nl80211;
using Bld.Libnl.Net.Nl80211.Enums;

using RunProcessAsTask;
using SharpPcap;
using SharpPcap.LibPcap;

namespace SendSinglePacketDemo;

internal class Program
{
    /// <summary>
    /// sudo iw wlan1 set monitor otherbss
    /// sudo iw wlan1 set channel 48
    /// </summary>
    static async Task Main(string[] args)
    {
        var device = LibPcapLiveDeviceList.Instance.Single(d => d.Name == "wlan1");
        device.OnPacketArrival += DeviceOnOnPacketArrival;
        device.Open(new DeviceConfiguration(){Mode = DeviceModes.Promiscuous, Monitor = MonitorMode.Active});
        device.Capture();
    }

    private static void DeviceOnOnPacketArrival(object sender, PacketCapture e)
    {
        var packet = e.GetPacket();
    }
}