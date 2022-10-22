using System.Net.NetworkInformation;
using SharpPcap;
using WiFiBroadcastNet.SystemHelpers;

namespace WiFiBroadcastNet;

public class Device
{
    private readonly ILiveDevice _pcapDevice;
    private readonly NetworkInterface _networkInterface;
    private readonly IOsCommandHelper _commandHelper;

    internal Device(
        ILiveDevice pcapDevice, 
        NetworkInterface networkInterface, 
        IOsCommandHelper commandHelper)
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
}