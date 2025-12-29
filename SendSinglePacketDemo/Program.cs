using System.Diagnostics;
using System.Net.NetworkInformation;
using Bld.Libnl.Net.Netlink;
using Bld.Libnl.Net.Nl80211;
using Bld.Libnl.Net.Nl80211.Enums;
using Bld.WlanUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PacketDotNet;
using PacketDotNet.Ieee80211;
using RunProcessAsTask;
using SharpPcap;
using SharpPcap.LibPcap;

namespace SendSinglePacketDemo;

internal class Program
{
    /// <summary>
    /// sudo iw wlx00c0caa98097 set monitor otherbss
    /// sudo iw dev wlx00c0caa98097 set freq 5180 HT20
    /// </summary>
    static async Task Main(string[] args)
    {
        var deviceName = "wlx00c0caa98097";

        var wlanManager = new WlanManager(NullLoggerFactory.Instance.CreateLogger<WlanManager>());
        // TODO: To fix
        //await wlanManager.TrySwitchToMonitorAsync();
        //await wlanManager.IwSetFrequencyAndChannelWidth(deviceName, Channels.Ch140, ChannelWidth._20MHz);

        var device = LibPcapLiveDeviceList.Instance.Single(d => d.Name == deviceName);

        device.Open(new DeviceConfiguration(){Mode = DeviceModes.Promiscuous});
        //device.OnPacketArrival += DeviceOnOnPacketArrival;
        //device.Capture();
        //Console.WriteLine("Press enter");
        //Console.ReadLine();
        //device.StopCapture();

        var dataDataFrame = new DataDataFrame()
        {
            FrameControl = { ToDS = false, FromDS = true },
            Duration = { Field = 0x1234 },
            DestinationAddress = PhysicalAddress.Parse("01:02:03:04:05:06"),
            SourceAddress = PhysicalAddress.Parse("07:08:09:0A:0B:0C"),
            PayloadData = new byte[] { 0x11, 0x12, 0x13, 0x14, 0x15 }
        };
        dataDataFrame.UpdateCalculatedValues();
        var radioPacket = new RadioPacket
        {
            PayloadPacket = dataDataFrame
        };
        radioPacket.UpdateCalculatedValues();

        Console.WriteLine("START SPAM");
        for (int i = 0; i < 2; i++)
        {
            device.SendPacket(radioPacket);
            await Task.Delay(100);
        }
        Console.WriteLine("FINISH");
    }

    private static void DeviceOnOnPacketArrival(object sender, PacketCapture e)
    {
        var rawPacket = e.GetPacket();
        var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
        var radioPacket = packet.Extract<RadioPacket>();

        if (radioPacket.HasPayloadPacket)
        {
            var payloadPacket = radioPacket.PayloadPacket;
            if (payloadPacket is DataDataFrame)
            {
                Console.WriteLine(radioPacket.PayloadPacket.GetType() + "    " + radioPacket.PayloadPacket.ToString());
            }
        }
        else
        {
            //Console.WriteLine(radioPacket.ToString());
        }
    }
}