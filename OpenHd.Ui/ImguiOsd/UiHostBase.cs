using System.Collections.Concurrent;
using Rtsp.Rtp;
using SharpVideo.Decoding;

namespace OpenHd.Ui.ImguiOsd;

internal abstract class UiHostBase<TDecoder, TDecoderOutputBuffer> : IUiHost
    where TDecoder: BaseDecoder<TDecoderOutputBuffer>, IDecoder
    where TDecoderOutputBuffer : class
{
    private readonly H264Payload _h264Depacketiser = new();
    private readonly BlockingCollection<RawMediaFrame> _frameQueue = new(boundedCapacity: 32);

    protected readonly CancellationTokenSource CancellationTokenSource = new();
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly ILogger Logger;
    protected readonly TDecoder H264Decoder;

    protected Task? DrawThread;
    private Task? _decodeThread;
    protected VideoFrameManager<TDecoder, TDecoderOutputBuffer>? VideoFrameManager;
    protected ImGuiUiRenderer? UiRenderer;

    protected abstract bool ShowDemoWindow { get; }

    protected UiHostBase(
        InMemoryPipeStreamAccessor h264Stream,
        TDecoder decoder,
        ILoggerFactory loggerFactory,
        ILogger logger)
    {
        LoggerFactory = loggerFactory;
        Logger = logger;

        H264Decoder = decoder;

        h264Stream.SetReceiveAction(ReceiveH624);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting {HostType}", GetType().Name);

        VideoFrameManager = new VideoFrameManager<TDecoder, TDecoderOutputBuffer>(
            H264Decoder,
            LoggerFactory.CreateLogger<VideoFrameManager<TDecoder, TDecoderOutputBuffer>>());

        UiRenderer = new ImGuiUiRenderer(
            LoggerFactory.CreateLogger<ImGuiUiRenderer>(),
            customRenderCallback: null,
            showDemoWindow: ShowDemoWindow);

        DrawThread = Task.Factory.StartNew(RunDrawThread, TaskCreationOptions.LongRunning);
        _decodeThread = Task.Factory.StartNew(RunDecodeThread, TaskCreationOptions.LongRunning);
        VideoFrameManager.Start();

        OnStart();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Stopping {HostType}", GetType().Name);
        await CancellationTokenSource.CancelAsync();

        _frameQueue.CompleteAdding();

        if (DrawThread != null)
        {
            await DrawThread;
        }

        if (_decodeThread != null)
        {
            await _decodeThread;
        }

        if (VideoFrameManager != null)
        {
            await VideoFrameManager.StopAsync();
            VideoFrameManager.Dispose();
        }

        OnStop();
    }

    /// <summary>
    /// Called after StartAsync completes initialization. Override for additional setup.
    /// </summary>
    protected virtual void OnStart() { }

    /// <summary>
    /// Called after StopAsync completes cleanup. Override for additional cleanup.
    /// </summary>
    protected virtual void OnStop() { }

    /// <summary>
    /// Main drawing thread implementation. Must be implemented by derived classes.
    /// </summary>
    protected abstract void RunDrawThread();

    private void ReceiveH624(ReadOnlyMemory<byte> payload)
    {
        var packet = new RtpPacket(payload.Span);
        var frame = _h264Depacketiser.ProcessPacket(packet);
        if (frame.Any())
        {
            try
            {
                _frameQueue.Add(frame);
            }
            catch (InvalidOperationException)
            {
                // Queue is completed, dispose the frame
                frame.Dispose();
            }
        }
    }

    private void RunDecodeThread()
    {
        try
        {
            foreach (var frame in _frameQueue.GetConsumingEnumerable(CancellationTokenSource.Token))
            {
                ProcessNalu(frame);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    private void ProcessNalu(RawMediaFrame frame)
    {
        foreach (var nalu in frame.Data)
        {
            // Directly decode the NALU - decoder manages buffers internally
            H264Decoder.Decode(nalu.Span);
        }

        frame.Dispose();
    }
}
