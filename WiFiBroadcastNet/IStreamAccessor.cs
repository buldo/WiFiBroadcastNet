namespace WiFiBroadcastNet;

public interface IStreamAccessor
{
    void ProcessIncomingFrame(ReadOnlyMemory<byte> payload);
}