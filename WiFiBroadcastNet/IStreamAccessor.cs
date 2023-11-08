namespace WiFiBroadcastNet;

public interface IStreamAccessor
{
    void ProcessIncomingFrame(Memory<byte> payload);
}