using SharpVideo.Rtp;

namespace OpenHd.Ui.ImguiOsd;

internal abstract partial class UiHostBase : IHostedService
{
    private readonly InMemoryPipeStreamAccessor _h264Stream;
    private readonly H264Depacketiser _h264Depacketiser = new();
    private readonly ILogger<UiHostBase> _logger;

    private long _counter;

    protected UiHostBase(
        InMemoryPipeStreamAccessor h264Stream,
        ILogger<UiHostBase> logger)
    {
        _h264Stream = h264Stream;
        _logger = logger;
        _h264Stream.SetReceiveAction(ReceiveH624);
    }

    public abstract Task StartAsync(CancellationToken cancellationToken);
    public abstract Task StopAsync(CancellationToken cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received {Count} frames")]
    private partial void LogFramesCount(long count);

    private void ReceiveH624(ReadOnlyMemory<byte> payload)
    {
        // TODO: Less array copies
        var packet = new RTPPacket(payload.ToArray());
        var hdr = packet.Header;
        var frame = _h264Depacketiser.ProcessRTPPayload(packet.Payload, hdr.SequenceNumber, hdr.Timestamp, hdr.MarkerBit, out var isKeyFrame);
        if (frame != null)
        {
            _counter++;
            if (_counter % 1000 == 0)
            {
                LogFramesCount(_counter);
            }
        }
    }
}