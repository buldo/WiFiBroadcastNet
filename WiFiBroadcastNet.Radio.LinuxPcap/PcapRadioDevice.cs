using Bld.Libnl;
using Bld.WlanUtils;
using PacketDotNet;
using PacketDotNet.Ieee80211;
using SharpPcap;
using WiFiBroadcastNet.Radio.Common;

namespace WiFiBroadcastNet.Radio.LinuxPcap;

public class PcapRadioDevice : IRadioDevice
{
    private readonly WlanDeviceInfo _deviceInfo;
    private readonly ILiveDevice _pcapDevice;
    private readonly WlanManager _wlanManager;

    private Action<RxFrame>? _consumeAction;

    internal PcapRadioDevice(
        WlanDeviceInfo deviceInfo,
        ILiveDevice pcapDevice,
        WlanManager wlanManager)
    {
        _deviceInfo = deviceInfo;
        _pcapDevice = pcapDevice;
        _wlanManager = wlanManager;
    }

    public void AttachDataConsumer(Action<RxFrame> receivedFramesChannel)
    {
        _consumeAction = receivedFramesChannel;
    }

    public void StartReceiving()
    {
        _pcapDevice.OnPacketArrival += PcapDeviceOnOnPacketArrival;
        _pcapDevice.Open(new DeviceConfiguration
        {
            Mode = DeviceModes.Promiscuous,
            Immediate = true,
            Monitor = MonitorMode.Inactive,
            LinkLayerType = LinkLayers.Ieee80211RadioTap
        });

        _pcapDevice.StartCapture();
    }

    public void SetChannelFrequency(ChannelFrequency channelFrequency)
    {
        _wlanManager.SetChannel(_deviceInfo, channelFrequency.Frequency, ChannelModes.ModeHt20);
    }

    private void PcapDeviceOnOnPacketArrival(object sender, PacketCapture e)
    {

        var rawPacket = e.GetPacket();
        var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
        var radioPacket = packet.Extract<RadioPacket>();

        var frame = new RxFrame()
        {
            Data = packet.PayloadPacket.Bytes
        };

        _consumeAction?.Invoke(frame);
    }
}
