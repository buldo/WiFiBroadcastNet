using Bld.Libnl;
using Bld.WlanUtils;

using SharpPcap;
using WiFiBroadcastNet.Radio.Common;

namespace WiFiBroadcastNet.Radio.LinuxPcap;

public class PcapRadioDevice : IRadioDevice
{
    private readonly WlanDeviceInfo _deviceInfo;
    private readonly ILiveDevice _pcapDevice;
    private readonly WlanManager _wlanManager;

    internal PcapRadioDevice(
        WlanDeviceInfo deviceInfo,
        ILiveDevice pcapDevice,
        WlanManager wlanManager)
    {
        _deviceInfo = deviceInfo;
        _pcapDevice = pcapDevice;
        _wlanManager = wlanManager;
    }

    public void Open()
    {
        _pcapDevice.Open(new DeviceConfiguration
        {
            Mode = DeviceModes.Promiscuous,
            Immediate = true
        });
    }

    public void AttachDataConsumer(Action<RxFrame> receivedFramesChannel)
    {
        throw new NotImplementedException();
    }

    public void StartReceiving()
    {
        throw new NotImplementedException();
    }

    public void SetChannelFrequency(ChannelFrequency channelFrequency)
    {
        _wlanManager.SetChannel(_deviceInfo, channelFrequency.Frequency, ChannelModes.ModeHt20);
    }
}