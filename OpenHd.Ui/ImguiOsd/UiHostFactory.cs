using System.Runtime.Versioning;
using SharpVideo.Decoding.V4l2.Stateless;
using SharpVideo.DmaBuffers;
using SharpVideo.Drm;
using SharpVideo.Utils;

namespace OpenHd.Ui.ImguiOsd;

internal class UiHostFactory
{
    private readonly InMemoryPipeStreamAccessor _h264Stream;
    private readonly DecodersFactory _decodersFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<UiHostFactory> _logger;

    public UiHostFactory(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        DecodersFactory decodersFactory,
        ILoggerFactory loggerFactory)
    {
        _h264Stream = h264Stream;
        _decodersFactory = decodersFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<UiHostFactory>();
    }

    public IUiHost CreateHost()
    {
        if (OperatingSystem.IsWindows())
        {
            return CreateWindowed();
        }

        if (OperatingSystem.IsLinux())
        {
            var dmExists = Environment.GetEnvironmentVariable("DISPLAY") != null || Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") != null;
            if (dmExists)
            {
                return CreateWindowed();
            }

            return CreateDrmHost();
        }

        // Fallback for other platforms
        return CreateWindowed();
    }

    [SupportedOSPlatform("linux")]
    private IUiHost CreateDrmHost()
    {
        _logger.LogInformation("Creating DRM host with V4L2 decoder");

        // For DRM mode, we need to create DrmBufferManager first
        // so V4L2 decoder can use it for zero-copy DMA buffer allocation
        var drmBufferManager = CreateDrmBufferManager();

        var v4l2Decoder = _decodersFactory.CreateV4l2Decoder(drmBufferManager);
        if (v4l2Decoder == null)
        {
            throw new InvalidOperationException("Failed to create V4L2 decoder for DRM mode");
        }

        return new DrmHost(
            _h264Stream,
            (V4l2H264StatelessDecoder)v4l2Decoder,
            _loggerFactory,
            _loggerFactory.CreateLogger<DrmHost>(),
            drmBufferManager: drmBufferManager);
    }

    [SupportedOSPlatform("linux")]
    private DrmBufferManager CreateDrmBufferManager()
    {
        _logger.LogInformation("Creating DRM buffer manager for V4L2 decoder");

        var drmDevice = DrmUtils.OpenDrmDevice(_logger);
        if (drmDevice == null)
        {
            throw new InvalidOperationException("Failed to open DRM device");
        }

        drmDevice.EnableDrmCapabilities(_logger);

        if (!DmaBuffersAllocator.TryCreate(out var dmaAllocator) || dmaAllocator == null)
        {
            drmDevice.Dispose();
            throw new InvalidOperationException("Failed to create DMA buffers allocator");
        }

        // Use NV12 format which is common for V4L2 decoders
        var bufferManager = new DrmBufferManager(
            drmDevice,
            dmaAllocator,
            [KnownPixelFormats.DRM_FORMAT_NV12, KnownPixelFormats.DRM_FORMAT_ARGB8888],
            _loggerFactory.CreateLogger<DrmBufferManager>());

        return bufferManager;
    }

    private IUiHost CreateWindowed()
    {
        var decoder = _decodersFactory.CreateFfmpegDecoder();

        return new WindowedHost(
            _h264Stream,
            decoder,
            _loggerFactory,
            _loggerFactory.CreateLogger<WindowedHost>());
    }
}