using System;

using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet.RadioStreams;

public class FecStream : IRadioStream
{
    private readonly IStreamAccessor _userStream;
    private readonly ILogger _logger;
    private readonly FECDecoder _fec;

    public FecStream(int id, IStreamAccessor userStream, ILogger logger)
    {
        _fec = new(logger, 1, FecConsts.MAX_TOTAL_FRAGMENTS_PER_BLOCK, true);
        _fec.mSendDecodedPayloadCallback = MSendDecodedPayloadCallback;
        _userStream = userStream;
        _logger = logger;
        Id = id;
    }

    private void MSendDecodedPayloadCallback(byte[] arg1)
    {
        _userStream.ProcessIncomingFrame(arg1);
    }

    public int Id { get; }

    public void ProcessFrame(Memory<byte> decryptedPayload)
    {
        if (!FECDecoder.validate_packet_size(decryptedPayload.Length))
        {
            _logger.LogDebug("invalid fec packet size {Size}", decryptedPayload.Length);
            return;
        }

        _fec.process_valid_packet(decryptedPayload.ToArray(), decryptedPayload.Length);
    }
}
