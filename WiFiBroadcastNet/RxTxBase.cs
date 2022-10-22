namespace WiFiBroadcastNet;

public abstract class RxTxBase
{
    private readonly List<Device> _devices = new();

    public void AddDevice(Device device)
    {
        _devices.Add(device);
    }

    public void Start()
    {
        foreach (var device in _devices)
        {
            device.PrepareOs();
        }
    }
}