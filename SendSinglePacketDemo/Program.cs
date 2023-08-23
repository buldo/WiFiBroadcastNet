using System.Diagnostics;

using Bld.Libnl.Net.Netlink;
using Bld.Libnl.Net.Nl80211;
using Bld.Libnl.Net.Nl80211.Enums;

using RunProcessAsTask;

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
        var devices = LibPcapLiveDeviceList.Instance.ToList();
        foreach (var device in devices)
        {
            Console.WriteLine($"{device.Name}");
        }
    }
}