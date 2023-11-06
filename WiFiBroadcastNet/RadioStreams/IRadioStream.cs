namespace WiFiBroadcastNet.RadioStreams;

internal interface IRadioStream
{
    public int Id { get; }

    void ProcessFrame(Memory<byte> decryptedPayload);
}