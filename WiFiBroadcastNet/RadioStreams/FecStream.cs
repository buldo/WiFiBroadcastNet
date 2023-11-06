using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet.RadioStreams;

public class FecStream : IRadioStream
{
    private readonly NormalFec _fec = new();

    public FecStream(int id)
    {
        Id = id;
    }

    public int Id { get; }

    public void ProcessFrame(Memory<byte> decryptedPayload)
    {
    }
}