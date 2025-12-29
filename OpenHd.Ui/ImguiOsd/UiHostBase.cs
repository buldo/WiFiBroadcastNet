using SharpVideo.Decoding;
using SharpVideo.Rtp;

namespace OpenHd.Ui.ImguiOsd;

internal abstract partial class UiHostBase : IHostedService
{
    private readonly InMemoryPipeStreamAccessor _h264Stream;
    private readonly H264Depacketiser _h264Depacketiser = new();
    private readonly ILogger<UiHostBase> _logger;
    private Task _decodingThread;

    protected readonly BaseDecoder H264Decoder;

    protected UiHostBase(
        InMemoryPipeStreamAccessor h264Stream,
        DecodersFactory decodersFactory,
        ILogger<UiHostBase> logger)
    {
        _h264Stream = h264Stream;
        H264Decoder = decodersFactory.CreateH264Decoder();
        H264Decoder.Start();
        _logger = logger;
        _h264Stream.SetReceiveAction(ReceiveH624);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        //_decodingThread = Task.Factory.StartNew(DecodingThread, TaskCreationOptions.LongRunning);
        Start();
        return Task.CompletedTask;
    }

    public abstract Task StopAsync(CancellationToken cancellationToken);

    protected abstract void Start();

    private void ReceiveH624(ReadOnlyMemory<byte> payload)
    {
        // TODO: Less array copies
        var packet = new RTPPacket(payload.ToArray());
        var hdr = packet.Header;
        var frame = _h264Depacketiser.ProcessRTPPayload(packet.Payload, hdr.SequenceNumber, hdr.Timestamp, hdr.MarkerBit, out var isKeyFrame);
        if (frame != null)
        {
            var nalu = frame.ToArray();
            ProcessNalu(nalu);
        }
    }

    private void ProcessNalu(ReadOnlySpan<byte> nalu)
    {
        var buffer = H264Decoder.GetEncodedBuffersForReuse();
        if (buffer == null)
        {
            _logger.LogWarning("Skipping frame");
            return;
        }

        if (buffer is ManagedMemoryEncodedBuffer memBuf)
        {
            memBuf.CopyFromSpan(nalu);
        }

        H264Decoder.AddBufferForDecode(buffer);
    }

    private unsafe void DecodingThread()
    {

    }
}