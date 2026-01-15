using System.Runtime.Versioning;
using OpenHd.Ui.Configuration;
using SharpVideo.Decoding.V4l2.Stateless;
using SharpVideo.Utils;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// DRM/KMS host for ImGui-based OSD rendering with hardware video plane.
/// Uses dual-plane architecture via DualPlanePresenter:
/// - OSD plane (GBM/OpenGL ES): ImGui interface with transparency, rendered on top
/// - Video plane (DMA buffers): Video frames from decoder, rendered below OSD
/// </summary>
/// <remarks>
/// Architecture:
/// - Single render loop manages both OSD rendering and video frame submission
/// - DualPlanePresenter handles atomic commits and z-ordering
/// - VideoOverlayManager bridges VideoFrameManager with the presenter
/// - OSD remains responsive even when no video frames are available
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class DrmHost : UiHostBase<V4l2H264StatelessDecoder, SharedDmaBuffer>
{
    /// <summary>
    /// Delay after warmup frame submission, in milliseconds.
    /// </summary>
    private const int WarmupDelayMs = 100;

    private readonly DrmHostConfiguration _configuration;
    private readonly DrmBufferManager _drmBufferManager;

    private DrmRenderingContext? _renderingContext;
    private VideoOverlayManager? _videoOverlayManager;

    protected override bool ShowDemoWindow => _configuration.ShowDemoWindow;

    public DrmHost(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        V4l2H264StatelessDecoder decoder,
        ILoggerFactory loggerFactory,
        ILogger<DrmHost> logger,
        DrmBufferManager drmBufferManager,
        DrmHostConfiguration? configuration = null)
        : base(h264Stream, decoder, loggerFactory, logger)
    {
        ArgumentNullException.ThrowIfNull(drmBufferManager);

        _drmBufferManager = drmBufferManager;
        _configuration = configuration ?? new DrmHostConfiguration();
        Logger.LogInformation("DrmHost initialized (dual-plane mode)");
    }

    protected override void RunDrawThread()
    {
        try
        {
            Logger.LogInformation("DrawThread started");

            // Set environment for DRM
            Environment.SetEnvironmentVariable("EGL_PLATFORM", "drm");

            InitializeResources();

            if (_renderingContext == null)
            {
                Logger.LogError("Failed to initialize DRM resources");
                return;
            }

            // Warmup frame
            _renderingContext.RenderWarmupFrame(RenderOsdFrame);
            Thread.Sleep(WarmupDelayMs);
            Logger.LogInformation("Display initialization completed");

            // Enter main loop
            Logger.LogInformation("Entering render loop");
            RunRenderLoop();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception in DrawThread");
        }
        finally
        {
            CleanupResources();
            Logger.LogInformation("DrawThread finished");
        }
    }

    private void InitializeResources()
    {
        var videoPixelFormat = H264Decoder.OutputPixelFormat;

        _renderingContext = DrmRenderingContext.Create(
            videoPixelFormat,
            _configuration,
            _drmBufferManager,
            LoggerFactory);

        _videoOverlayManager = new VideoOverlayManager(
            _renderingContext.Presenter,
            VideoFrameManager!,
            UpdateFrameStatistics,
            LoggerFactory);

        Logger.LogInformation("DRM resources initialized successfully (dual-plane mode)");
    }

    private void RunRenderLoop()
    {
        var frameCount = 0;

        while (!_renderingContext!.ExitRequested && !CancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Process input events
                _renderingContext.ProcessInput();

                if (_renderingContext.ExitRequested)
                {
                    break;
                }

                // Submit latest video frame to the video plane (if available)
                _videoOverlayManager!.TrySubmitLatestFrame();

                // Process completed video frames (return to decoder)
                _videoOverlayManager.ProcessCompletedFrames();

                // Render and submit OSD frame
                if (_renderingContext.RenderOsdFrame(RenderOsdFrame))
                {
                    frameCount++;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception in render loop on frame {Frame}", frameCount);
                break;
            }
        }

        Logger.LogInformation("Render loop exited after {FrameCount} OSD frames", frameCount);
    }

    private void UpdateFrameStatistics(SharedDmaBuffer frame)
    {
        UiRenderer?.UpdateFrameStatistics(
            (int)frame.Width,
            (int)frame.Height,
            0, // V4L2 doesn't use AVPixelFormat
            0, // No PTS available from V4L2
            false); // Key frame detection not available
    }

    private void RenderOsdFrame(float deltaTime)
    {
        UiRenderer?.RenderUi(deltaTime);
    }

    private void CleanupResources()
    {
        Logger.LogInformation("Cleaning up DRM resources");

        _videoOverlayManager?.Dispose();
        _videoOverlayManager = null;

        _renderingContext?.Dispose();
        _renderingContext = null;

        // DrmBufferManager is owned by DrmHost and must be disposed last
        // (after decoder stops using it)
        _drmBufferManager.Dispose();
    }
}
