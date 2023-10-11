namespace WiFiBroadcastNet.Devices;

public interface IDevicesProvider
{
    public List<IRadioDevice> GetDevices();
}