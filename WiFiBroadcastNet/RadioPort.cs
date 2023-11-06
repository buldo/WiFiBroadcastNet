namespace WiFiBroadcastNet;

public struct RadioPort
{
    public required bool Encrypted { get; init; }

    public required byte MultiplexIndex { get; init; }

    public static RadioPort FromByte(byte value)
    {
        return new RadioPort
        {
            Encrypted = (value & 0b0000_0001) == 0b0000_0001,
            MultiplexIndex = (byte)((value >> 1))
        };
    }
}