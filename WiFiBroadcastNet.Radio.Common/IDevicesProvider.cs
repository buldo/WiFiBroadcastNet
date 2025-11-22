namespace WiFiBroadcastNet.Radio.Common;

public interface IDevicesProvider
{
    public List<IRadioDevice> GetDevices();
}