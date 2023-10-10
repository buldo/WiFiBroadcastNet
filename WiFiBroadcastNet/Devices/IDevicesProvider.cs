namespace WiFiBroadcastNet.Devices;

public interface IDevicesProvider
{
    public List<IDevice> GetDevices();
}