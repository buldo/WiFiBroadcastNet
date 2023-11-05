namespace WiFiBroadcastNet;

public struct RadioPort
{
    public required bool Encrypted { get; init; }

    public required byte MultiplexIndex { get; init; }

    public static RadioPort FromByte(byte value)
    {
        return new RadioPort
        {
            Encrypted = (value & 0b1000_0000) !=0,
            MultiplexIndex = (byte)(value & 0b0111_1111)
        };
    }
}