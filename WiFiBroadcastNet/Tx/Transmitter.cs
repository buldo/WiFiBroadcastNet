using WiFiBroadcastNet.Devices;

namespace WiFiBroadcastNet.Tx;

public class Transmitter
{
    private readonly PcapDevice _pcapDevice;

    public Transmitter(PcapDevice pcapDevice)
    {
        _pcapDevice = pcapDevice;
    }

    public void Start()
    {
        _pcapDevice.Open();
    }

    public void Send(byte[] bytes)
    {
        throw new NotImplementedException();
    }
}