using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using SharpVideo.Decoding;
using SharpVideo.DmaBuffers;
using SharpVideo.Drm;
using SharpVideo.Utils;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Manages video frame output to DRM overlay plane using DMA buffers.
/// Converts YUV420P frames from FFmpeg to NV12 format for DRM display.
/// Automatically handles video resolution changes by reallocating buffers.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class VideoPlaneRenderer : IDisposable
{
    private readonly DrmPlaneLastDmaBufferPresenter _overlayPresenter;
    private readonly DrmBufferManager _bufferManager;
    private readonly ILogger<VideoPlaneRenderer> _logger;
    private readonly int _bufferCount;

    private readonly List<SharedDmaBuffer> _buffers = [];
    private int _currentBufferIndex;
    private uint _currentVideoWidth;
    private uint _currentVideoHeight;
    private bool _disposed;

    public VideoPlaneRenderer(
        DrmPlaneLastDmaBufferPresenter overlayPresenter,
        DrmBufferManager bufferManager,
        ILogger<VideoPlaneRenderer> logger,
        int bufferCount = 3)
    {
        _overlayPresenter = overlayPresenter ?? throw new ArgumentNullException(nameof(overlayPresenter));
        _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bufferCount = bufferCount;

        _logger.LogInformation("VideoPlaneRenderer created (buffers will be allocated on first frame)");
    }

    /// <summary>
    /// Renders a decoded FFmpeg frame to the overlay plane.
    /// Converts YUV420P to NV12 and presents via DRM.
    /// Handles resolution changes automatically.
    /// </summary>
    public unsafe void RenderFrame(FfmpegDecodedFrame frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);

        var avFrame = frame.Frame;
        if (avFrame == null)
        {
            return;
        }

        var frameWidth = (uint)avFrame->width;
        var frameHeight = (uint)avFrame->height;

        // Check if we need to (re)allocate buffers for this resolution
        if (frameWidth != _currentVideoWidth || frameHeight != _currentVideoHeight)
        {
            ReallocateBuffers(frameWidth, frameHeight);
        }

        if (_buffers.Count == 0)
        {
            _logger.LogWarning("No buffers available for rendering");
            return;
        }

        // Get next buffer
        var buffer = _buffers[_currentBufferIndex];
        _currentBufferIndex = (_currentBufferIndex + 1) % _buffers.Count;

        // Convert YUV420P to NV12 and copy to DMA buffer
        ConvertYuv420pToNv12(avFrame, buffer);

        // Sync the buffer to ensure GPU/display can see the data
        buffer.DmaBuffer.SyncMap();

        // Present on overlay plane
        _overlayPresenter.SetOverlayPlaneBuffer(buffer);

        // Return completed buffers (they will be reused automatically)
        _ = _overlayPresenter.GetPresentedOverlayBuffers();
    }

    private void ReallocateBuffers(uint width, uint height)
    {
        _logger.LogInformation(
            "Video resolution changed: {OldWidth}x{OldHeight} -> {NewWidth}x{NewHeight}, reallocating buffers",
            _currentVideoWidth, _currentVideoHeight, width, height);

        // Free existing buffers
        FreeBuffers();

        // Allocate new buffers with video resolution
        _logger.LogInformation("Allocating {Count} NV12 buffers for video ({Width}x{Height})",
            _bufferCount, width, height);

        for (int i = 0; i < _bufferCount; i++)
        {
            var buffer = _bufferManager.AllocateBuffer(width, height, KnownPixelFormats.DRM_FORMAT_NV12);
            buffer.MapBuffer();

            if (buffer.MapStatus == MapStatus.FailedToMap)
            {
                _logger.LogError("Failed to map buffer {Index}", i);
                throw new InvalidOperationException($"Failed to map DMA buffer {i}");
            }

            _buffers.Add(buffer);
        }

        _currentVideoWidth = width;
        _currentVideoHeight = height;
        _currentBufferIndex = 0;

        _logger.LogInformation("Video plane buffers allocated successfully");
    }

    private void FreeBuffers()
    {
        foreach (var buffer in _buffers)
        {
            try
            {
                buffer.DmaBuffer.UnmapBuffer();
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing buffer");
            }
        }

        _buffers.Clear();
    }

    /// <summary>
    /// Converts YUV420P (planar) to NV12 (semi-planar) format.
    /// YUV420P: Y plane, U plane, V plane (all separate)
    /// NV12: Y plane, UV interleaved plane
    /// </summary>
    private unsafe void ConvertYuv420pToNv12(FFmpeg.AutoGen.AVFrame* avFrame, SharedDmaBuffer buffer)
    {
        var frameWidth = avFrame->width;
        var frameHeight = avFrame->height;

        var dstSpan = buffer.DmaBuffer.GetMappedSpan();
        var dstStride = buffer.Stride;

        // Calculate NV12 plane offsets
        var yPlaneSize = dstStride * buffer.Height;
        var uvPlaneOffset = yPlaneSize;

        // Source pointers and strides
        var srcY = avFrame->data[0];
        var srcU = avFrame->data[1];
        var srcV = avFrame->data[2];
        var srcStrideY = avFrame->linesize[0];
        var srcStrideU = avFrame->linesize[1];
        var srcStrideV = avFrame->linesize[2];

        fixed (byte* dstPtr = dstSpan)
        {
            // Copy Y plane
            for (int y = 0; y < frameHeight; y++)
            {
                var srcRow = srcY + y * srcStrideY;
                var dstRow = dstPtr + y * dstStride;

                Buffer.MemoryCopy(srcRow, dstRow, dstStride, frameWidth);
            }

            // Interleave U and V planes to create UV plane
            var uvHeight = frameHeight / 2;
            var uvWidth = frameWidth / 2;

            for (int y = 0; y < uvHeight; y++)
            {
                var srcRowU = srcU + y * srcStrideU;
                var srcRowV = srcV + y * srcStrideV;
                var dstRow = dstPtr + uvPlaneOffset + y * dstStride;

                for (int x = 0; x < uvWidth; x++)
                {
                    dstRow[x * 2] = srcRowU[x];      // U
                    dstRow[x * 2 + 1] = srcRowV[x];  // V
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing video plane renderer");

        FreeBuffers();

        _disposed = true;
    }
}
