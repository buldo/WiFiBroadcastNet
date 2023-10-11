using System.Net.NetworkInformation;
using System.Threading.Channels;

using Bld.WlanUtils;

using SharpPcap;
using WiFiBroadcastNet.SystemHelpers;

namespace WiFiBroadcastNet.Devices;

public class PcapRadioDevice : IRadioDevice
{
    private readonly ILiveDevice _pcapDevice;
    private readonly NetworkInterface _networkInterface;
    private readonly LinuxHelpers _commandHelper;

    internal PcapRadioDevice(
        ILiveDevice pcapDevice,
        NetworkInterface networkInterface,
        LinuxHelpers commandHelper)
    {
        _pcapDevice = pcapDevice;
        _networkInterface = networkInterface;
        _commandHelper = commandHelper;
    }

    public void PrepareOs()
    {
        _commandHelper.SetUnmanagedMode(_networkInterface.Name);
        _commandHelper.SetMonitorMode(_networkInterface.Name);
    }

    public void SetFrequency(Frequency frequency)
    {
        _commandHelper.SetFrequency(_networkInterface.Name, frequency);
    }

    public void Open()
    {
        _pcapDevice.Open(new DeviceConfiguration
        {
            Mode = DeviceModes.Promiscuous,
            Immediate = true
        });
    }

    public void StartReceiving(ChannelWriter<RxFrame> receivedFramesChannel)
    {
        throw new NotImplementedException();
    }

    public void SetChannel(WlanChannel channel)
    {
        throw new NotImplementedException();
    }
}