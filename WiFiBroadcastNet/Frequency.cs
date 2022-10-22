namespace WiFiBroadcastNet;

public class Frequency
{
    public uint ValueInMHz { get; }
    
    public uint Channel { get; }

    public Frequency(uint channel, uint valueInMHz)
    {
        Channel = channel;
        ValueInMHz = valueInMHz;
    }
}