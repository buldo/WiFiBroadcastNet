namespace WiFiBroadcastNet.Tx;

public class Transmitter
{
    private readonly Device _device;

    public Transmitter(Device device)
    {
        _device = device;
    }

    public void Start()
    {
        _device.Open();
    }

    public void Send(byte[] bytes)
    {
        throw new NotImplementedException();
    }
}