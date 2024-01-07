namespace WiFiBroadcastNet.RadioStreams;

internal interface IRadioStream
{
    public int Id { get; }

    void ProcessFrame(ReadOnlyMemory<byte> decryptedPayload);
}