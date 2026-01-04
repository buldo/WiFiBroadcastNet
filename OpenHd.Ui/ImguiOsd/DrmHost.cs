using System.Runtime.Versioning;
using FFmpeg.AutoGen;
using Hexa.NET.ImGui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpVideo.Decoding;
using SharpVideo.Decoding.V4l2;
using SharpVideo.DmaBuffers;
using SharpVideo.Drm;
using SharpVideo.Gbm;
using SharpVideo.ImGui;
using SharpVideo.Linux.Native;
using SharpVideo.Linux.Native.C;
using SharpVideo.Rtp;
using SharpVideo.Utils;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// DRM/KMS host for ImGui-based OSD rendering with hardware video plane.
/// Uses dual-plane architecture:
/// - Primary plane (GBM/OpenGL ES): ImGui OSD with transparency
/// - Overlay plane (DMA buffers): Video frames from decoder
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class DrmHost : UiHostBase
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ILogger<DrmHost> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DrmHostConfiguration _configuration;

    private Task? _drawThread;
    private VideoFrameManager? _videoFrameManager;
    private ImGuiUiRenderer? _uiRenderer;

    // DRM resources
    private DrmDevice? _drmDevice;
    private GbmDevice? _gbmDevice;
    private DmaBuffersAllocator? _dmaAllocator;
    private DrmBufferManager? _drmBufferManager;
    private DrmPresenter? _presenter;
    private InputManager? _inputManager;
    private ImGuiManager? _imguiManager;
    private VideoPlaneRenderer? _videoPlaneRenderer;
    private readonly Dictionary<SharedDmaBuffer, UniversalDecodedFrame> _framesInUseByDrm = new();

    public DrmHost(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        DecodersFactory decodersFactory,
        ILoggerFactory loggerFactory,
        ILogger<DrmHost> logger,
        DrmHostConfiguration? configuration = null)
        : base(h264Stream, decodersFactory, logger)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration ?? new DrmHostConfiguration();
        _logger.LogInformation("DrmHost initialized (dual-plane mode)");
    }

    protected override void Start()
    {
        _logger.LogInformation("Starting DrmHost");

        _videoFrameManager = new VideoFrameManager(
            H264Decoder,
            _loggerFactory.CreateLogger<VideoFrameManager>());

        _uiRenderer = new ImGuiUiRenderer(
            _loggerFactory.CreateLogger<ImGuiUiRenderer>(),
            customRenderCallback: null,
            showDemoWindow: _configuration.ShowDemoWindow);

        _drawThread = Task.Factory.StartNew(DrawThread, TaskCreationOptions.LongRunning);
        _videoFrameManager.Start();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DrmHost");
        await _cancellationTokenSource.CancelAsync();

        if (_drawThread != null)
        {
            await _drawThread;
        }

        if (_videoFrameManager != null)
        {
            await _videoFrameManager.StopAsync();
            _videoFrameManager.Dispose();
        }
    }

    private void DrawThread()
    {
        try
        {
            _logger.LogInformation("DrawThread started");

            // Set environment for DRM
            Environment.SetEnvironmentVariable("EGL_PLATFORM", "drm");

            InitializeDrmResources();

            if (_presenter == null || _imguiManager == null)
            {
                _logger.LogError("Failed to initialize DRM resources");
                return;
            }

            // Warmup frame
            _logger.LogInformation("Rendering warmup frame...");
            var gbmAtomicPresenter = _presenter.AsGbmAtomicPresenter();
            if (gbmAtomicPresenter != null && _imguiManager.WarmupFrame(RenderOsdFrame))
            {
                if (gbmAtomicPresenter.SubmitFrame())
                {
                    _logger.LogInformation("Warmup frame submitted");
                }
            }

            Thread.Sleep(100);
            _logger.LogInformation("Display initialization completed");

            // Enter main loop
            _logger.LogInformation("Entering render loop");
            RunRenderLoop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DrawThread");
        }
        finally
        {
            CleanupResources();
            _logger.LogInformation("DrawThread finished");
        }
    }

    /// <summary>
    /// Initializes all DRM resources including overlay plane.
    /// Uses decoder's OutputPixelFormat directly for the video plane.
    /// </summary>
    private void InitializeDrmResources()
    {
        // Get decoder's output format - DRM plane must support this format
        var videoPixelFormat = H264Decoder.OutputPixelFormat;

        _logger.LogInformation(
            "Initializing DRM resources. Video pixel format: {Format}",
            videoPixelFormat.GetName());

        // Open DRM device
        _drmDevice = string.IsNullOrEmpty(_configuration.DrmDevicePath)
            ? DrmUtils.OpenDrmDevice(_logger)
            : DrmDevice.Open(_configuration.DrmDevicePath);

        if (_drmDevice == null)
        {
            throw new InvalidOperationException("Failed to open DRM device");
        }

        _drmDevice.EnableDrmCapabilities(_logger);

        // Create GBM device for ImGui rendering
        _gbmDevice = GbmDevice.CreateFromDrmDevice(_drmDevice);
        _logger.LogInformation("Created GBM device for ImGui rendering");

        // Create DMA buffer allocator for video plane
        if (!DmaBuffersAllocator.TryCreate(out _dmaAllocator) || _dmaAllocator == null)
        {
            throw new InvalidOperationException("Failed to create DMA buffers allocator");
        }

        // Initialize DrmBufferManager with formats needed for this decoder
        _drmBufferManager = new DrmBufferManager(
            _drmDevice,
            _dmaAllocator,
            [videoPixelFormat, KnownPixelFormats.DRM_FORMAT_ARGB8888],
            _loggerFactory.CreateLogger<DrmBufferManager>());

        // Create unified DRM presenter with GBM atomic primary (ImGui) and DMA overlay (video)
        _presenter = DrmPresenter.CreateWithGbmAtomicAndDmaOverlay(
            _drmDevice,
            _configuration.DisplayWidth,
            _configuration.DisplayHeight,
            _gbmDevice,
            _drmBufferManager,
            KnownPixelFormats.DRM_FORMAT_ARGB8888,  // Primary plane for ImGui OSD
            videoPixelFormat,                      // Overlay plane for video (matches decoder)
            _logger);

        if (_presenter == null)
        {
            throw new InvalidOperationException("Failed to create DRM presenter");
        }

        // Get actual display dimensions (may differ from requested if fallback mode was used)
        var actualWidth = _presenter.PrimaryPlanePresenter.Width;
        var actualHeight = _presenter.PrimaryPlanePresenter.Height;

        if (actualWidth != _configuration.DisplayWidth || actualHeight != _configuration.DisplayHeight)
        {
            _logger.LogWarning(
                "Display mode differs from requested: requested {ReqWidth}x{ReqHeight}, actual {ActWidth}x{ActHeight}",
                _configuration.DisplayWidth, _configuration.DisplayHeight, actualWidth, actualHeight);
        }

        _logger.LogInformation("Created dual-plane DRM presenter ({Width}x{Height})", actualWidth, actualHeight);

        // Configure z-order: Primary plane (ImGui OSD) on top, Overlay plane (video) below
        ConfigurePlaneZOrder();

        // Initialize input manager
        if (_configuration.EnableInput)
        {
            _inputManager = new InputManager(
                actualWidth,
                actualHeight,
                _loggerFactory.CreateLogger<InputManager>());
            _logger.LogInformation("Input manager initialized");
        }

        // Get GBM atomic presenter for ImGui
        var gbmAtomicPresenter = _presenter.AsGbmAtomicPresenter();
        if (gbmAtomicPresenter == null)
        {
            throw new InvalidOperationException("Failed to get GBM atomic presenter");
        }

        // Configure ImGui with actual display dimensions
        var imguiConfig = new ImGuiDrmConfiguration
        {
            Width = actualWidth,
            Height = actualHeight,
            DrmDevice = _drmDevice,
            GbmDevice = _gbmDevice,
            GbmSurfaceHandle = gbmAtomicPresenter.GetNativeGbmSurfaceHandle(),
            PixelFormat = KnownPixelFormats.DRM_FORMAT_ARGB8888,
            ConfigFlags = ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.DockingEnable,
            DrawCursor = true,
            UiScale = _configuration.UiScale,
            GlslVersion = "#version 300 es",
            EnableInput = _configuration.EnableInput
        };

        _imguiManager = new ImGuiManager(
            imguiConfig,
            _inputManager,
            _loggerFactory.CreateLogger<ImGuiManager>());

        _logger.LogInformation("ImGui manager initialized");

        // Create video plane renderer for overlay
        _videoPlaneRenderer = new VideoPlaneRenderer(
            _presenter.OverlayPlanePresenter,
            _drmBufferManager,
            videoPixelFormat,
            _loggerFactory.CreateLogger<VideoPlaneRenderer>());

        _logger.LogInformation("DRM resources initialized successfully (dual-plane mode)");
    }

    private void ConfigurePlaneZOrder()
    {
        if (_presenter == null)
        {
            return;
        }

        _logger.LogInformation("Configuring plane z-order...");

        var primaryZposRange = _presenter.PrimaryPlane.GetPlaneZPositionRange();
        var overlayZposRange = _presenter.OverlayPlane.GetPlaneZPositionRange();

        if (primaryZposRange.HasValue)
        {
            _logger.LogInformation("Primary plane zpos range: [{Min}, {Max}], current: {Current}",
                primaryZposRange.Value.min, primaryZposRange.Value.max, primaryZposRange.Value.current);
        }
        else
        {
            _logger.LogWarning("Primary plane does not support zpos property");
        }

        if (overlayZposRange.HasValue)
        {
            _logger.LogInformation("Overlay plane zpos range: [{Min}, {Max}], current: {Current}",
                overlayZposRange.Value.min, overlayZposRange.Value.max, overlayZposRange.Value.current);
        }
        else
        {
            _logger.LogWarning("Overlay plane does not support zpos property");
        }

        // Set z-position to make primary plane (OSD) appear on top of overlay (video)
        if (primaryZposRange.HasValue && overlayZposRange.HasValue)
        {
            var primaryZpos = primaryZposRange.Value.max;
            var overlayZpos = overlayZposRange.Value.min;

            _logger.LogInformation("Setting Primary zpos={PrimaryZpos} (OSD on top), Overlay zpos={OverlayZpos} (video below)",
                primaryZpos, overlayZpos);

            var primarySuccess = _presenter.PrimaryPlane.SetPlaneZPosition(primaryZpos);
            var overlaySuccess = _presenter.OverlayPlane.SetPlaneZPosition(overlayZpos);

            if (primarySuccess && overlaySuccess)
            {
                _logger.LogInformation("Z-positioning successful: OSD will render on top of video");
            }
            else
            {
                _logger.LogWarning("Failed to set z-positions - OSD may not appear on top of video");
            }
        }
    }

    private void RunRenderLoop()
    {
        var exiting = false;
        var frameCount = 0;
        var inputFd = _inputManager?.GetFileDescriptor() ?? -1;
        var gbmAtomicPresenter = _presenter?.AsGbmAtomicPresenter();

        if (gbmAtomicPresenter == null)
        {
            _logger.LogError("Failed to get GBM atomic presenter for render loop");
            return;
        }

        while (!exiting && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Poll input events (non-blocking)
                if (_inputManager != null && inputFd >= 0)
                {
                    var pollFd = new PollFd
                    {
                        fd = inputFd,
                        events = PollEvents.POLLIN
                    };

                    var pollResult = Libc.poll(ref pollFd, 1, 0);
                    if (pollResult > 0)
                    {
                        _inputManager.ProcessEvents();
                    }

                    // Check for ESC key to exit
                    if (_inputManager.IsKeyDown(1)) // KEY_ESC = 1
                    {
                        _logger.LogInformation("ESC key pressed, exiting");
                        exiting = true;
                        continue;
                    }
                }

                // Render video frame to overlay plane
                RenderVideoFrame();

                // Render OSD frame to primary plane
                var osdFrameRendered = _imguiManager!.RenderFrame(RenderOsdFrame);

                if (osdFrameRendered)
                {
                    if (gbmAtomicPresenter.SubmitFrame())
                    {
                        frameCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in render loop on frame {Frame}", frameCount);
                exiting = true;
            }
        }

        _logger.LogInformation("Render loop exited after {FrameCount} frames", frameCount);
    }

    private void RenderVideoFrame()
    {
        // Release buffers that DRM has finished displaying
        ReleaseCompletedFrames();

        var frame = _videoFrameManager?.AcquireCurrentFrame();
        if (frame != null)
        {
            _logger.LogTrace("Rendering video frame to overlay plane");

            // Update UI statistics based on frame type
            UpdateFrameStatistics(frame);

            // If it's a V4L2 DMA-BUF frame, we must hold it until DRM finishes displaying it
            if (frame is V4l2DecodedFrame { IsDmaBuf: true, DmaBuffer: not null } v4l2Frame)
            {
                lock (_framesInUseByDrm)
                {
                    _framesInUseByDrm[v4l2Frame.DmaBuffer] = frame;
                }
                _videoPlaneRenderer?.RenderFrame(frame);
            }
            else
            {
                // For other frame types (FFmpeg or MMAP), a copy occurs,
                // so the original frame can be released immediately
                _videoPlaneRenderer?.RenderFrame(frame);
                _videoFrameManager?.ReleaseFrame(frame);
            }
            
            _logger.LogTrace("Video frame presented");
        }
    }

    private void ReleaseCompletedFrames()
    {
        if (_presenter?.OverlayPlanePresenter == null) return;

        var completedBuffers = _presenter.OverlayPlanePresenter.GetPresentedOverlayBuffers();
        if (completedBuffers.Length > 0)
        {
            lock (_framesInUseByDrm)
            {
                foreach (var buffer in completedBuffers)
                {
                    if (_framesInUseByDrm.Remove(buffer, out var frameToRelease))
                    {
                        _videoFrameManager?.ReleaseFrame(frameToRelease);
                    }
                }
            }
        }
    }

    private unsafe void UpdateFrameStatistics(UniversalDecodedFrame frame)
    {
        switch (frame)
        {
            case FfmpegDecodedFrame ffmpegFrame:
                var avFrame = ffmpegFrame.Frame;
                if (avFrame != null)
                {
                    _uiRenderer?.UpdateFrameStatistics(
                        avFrame->width,
                        avFrame->height,
                        avFrame->format,
                        avFrame->pts,
                        (avFrame->flags & ffmpeg.AV_FRAME_FLAG_KEY) != 0);
                }
                break;
            case V4l2DecodedFrame v4l2Frame:
                _uiRenderer?.UpdateFrameStatistics(
                    (int)v4l2Frame.Width,
                    (int)v4l2Frame.Height,
                    0, // V4L2 doesn't use AVPixelFormat
                    0, // No PTS available from V4L2
                    false); // Key frame detection not available
                break;
        }
    }

    private void RenderOsdFrame(float deltaTime)
    {
        // Render ImGui UI (OSD)
        _uiRenderer?.RenderUi(deltaTime);
    }

    private void CleanupResources()
    {
        _logger.LogInformation("Cleaning up DRM resources");

        // Release any frames still in use by DRM
        lock (_framesInUseByDrm)
        {
            foreach (var frame in _framesInUseByDrm.Values)
            {
                _videoFrameManager?.ReleaseFrame(frame);
            }
            _framesInUseByDrm.Clear();
        }

        _videoPlaneRenderer?.Dispose();
        _imguiManager?.Dispose();
        _inputManager?.Dispose();
        _presenter?.Dispose();
        _drmBufferManager?.Dispose();
        _gbmDevice?.Dispose();
        _drmDevice?.Dispose();
    }
}
