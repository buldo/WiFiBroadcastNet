namespace WiFiBroadcastNet.Rx;

public class Receiver
{
    private List<Device> _devices;

    public Receiver(IEnumerable<Device> devices)
    {
        _devices = devices.ToList();
    }

    public void Start()
    {
        foreach (var device in _devices)
        {
            device.Open();
        }
    }
}