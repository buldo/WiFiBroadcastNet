using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet.RadioStreams;

public class FecStream : IRadioStream
{
    private readonly IStreamAccessor _userStream;
    private readonly FECDecoder _fec;

    public FecStream(int id, IStreamAccessor userStream, ILogger logger)
    {
        _fec = new(logger, 1, FecConsts.MAX_TOTAL_FRAGMENTS_PER_BLOCK, true);
        _userStream = userStream;
        Id = id;
    }

    public int Id { get; }

    public void ProcessFrame(Memory<byte> decryptedPayload)
    {
        _fec.process_valid_packet(decryptedPayload.ToArray(), decryptedPayload.Length);
    }
}
