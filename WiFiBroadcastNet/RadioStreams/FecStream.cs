using System;

using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet.RadioStreams;

public class FecStream : IRadioStream
{
    private readonly IStreamAccessor _userStream;
    private readonly ILogger _logger;
    private readonly FecDecoder _fec;

    public FecStream(int id, IStreamAccessor userStream, ILogger logger)
    {
        _fec = new(logger, 1, FecConsts.MAX_TOTAL_FRAGMENTS_PER_BLOCK, true);
        _fec._sendDecodedPayloadCallback = MSendDecodedPayloadCallback;
        _userStream = userStream;
        _logger = logger;
        Id = id;
    }

    private void MSendDecodedPayloadCallback(byte[] arg1)
    {
        _userStream.ProcessIncomingFrame(arg1);
    }

    public int Id { get; }

    public void ProcessFrame(ReadOnlyMemory<byte> decryptedPayload)
    {
        if (!FecDecoder.ValidatePacketSize(decryptedPayload.Length))
        {
            _logger.LogDebug("invalid fec packet size {Size}", decryptedPayload.Length);
            return;
        }

        _fec.ProcessValidPacket(decryptedPayload.Span);
    }
}
