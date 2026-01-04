using System.Runtime.Versioning;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using SharpVideo.Decoding;
using SharpVideo.Decoding.V4l2;
using SharpVideo.DmaBuffers;
using SharpVideo.Drm;
using SharpVideo.Utils;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Manages video frame output to DRM overlay plane using DMA buffers.
/// Supports both FFmpeg frames (with copy) and V4L2 DMA-BUF frames (zero-copy).
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class VideoPlaneRenderer : IDisposable
{
    private readonly DrmPlaneLastDmaBufferPresenter _overlayPresenter;
    private readonly DrmBufferManager _bufferManager;
    private readonly ILogger<VideoPlaneRenderer> _logger;
    private readonly int _bufferCount;
    private readonly PixelFormat _pixelFormat;

    private readonly List<SharedDmaBuffer> _buffers = [];
    private int _currentBufferIndex;
    private uint _currentVideoWidth;
    private uint _currentVideoHeight;
    private bool _disposed;

    /// <summary>
    /// Creates a new VideoPlaneRenderer with specified pixel format.
    /// </summary>
    /// <param name="overlayPresenter">DRM overlay plane presenter</param>
    /// <param name="bufferManager">Buffer manager for DMA buffer allocation</param>
    /// <param name="pixelFormat">Pixel format for both decoder output and DRM plane</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="bufferCount">Number of buffers to allocate (for FFmpeg copy mode)</param>
    public VideoPlaneRenderer(
        DrmPlaneLastDmaBufferPresenter overlayPresenter,
        DrmBufferManager bufferManager,
        PixelFormat pixelFormat,
        ILogger<VideoPlaneRenderer> logger,
        int bufferCount = 3)
    {
        ArgumentNullException.ThrowIfNull(overlayPresenter);
        ArgumentNullException.ThrowIfNull(bufferManager);
        ArgumentNullException.ThrowIfNull(logger);

        _overlayPresenter = overlayPresenter;
        _bufferManager = bufferManager;
        _logger = logger;
        _bufferCount = bufferCount;
        _pixelFormat = pixelFormat;

        _logger.LogInformation(
            "VideoPlaneRenderer created with format: {Format}",
            pixelFormat.GetName());
    }

    /// <summary>
    /// Renders a decoded frame to the overlay plane.
    /// Automatically selects zero-copy or copy path based on frame type.
    /// </summary>
    public void RenderFrame(UniversalDecodedFrame frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);

        switch (frame)
        {
            case V4l2DecodedFrame v4l2Frame:
                RenderV4l2Frame(v4l2Frame);
                break;
            case FfmpegDecodedFrame ffmpegFrame:
                RenderFfmpegFrame(ffmpegFrame);
                break;
            default:
                _logger.LogWarning("Unsupported frame type: {Type}", frame.GetType().Name);
                break;
        }
    }

    /// <summary>
    /// Renders V4L2 frame. Uses zero-copy for DMA-BUF, copy for MMAP.
    /// </summary>
    private void RenderV4l2Frame(V4l2DecodedFrame frame)
    {
        if (frame.IsDmaBuf && frame.DmaBuffer is not null)
        {
            // Zero-copy path: V4L2 decoder already output to DMA buffer
            _logger.LogTrace("Zero-copy V4L2 DMA-BUF frame to overlay plane");
            _overlayPresenter.SetOverlayPlaneBuffer(frame.DmaBuffer);
        }
        else if (frame.MmapBuffer is not null)
        {
            // MMAP path: need to copy to DMA buffer
            RenderV4l2MmapFrame(frame);
        }
    }

    /// <summary>
    /// Renders V4L2 MMAP frame by copying to DMA buffer.
    /// </summary>
    private void RenderV4l2MmapFrame(V4l2DecodedFrame frame)
    {
        if (frame.MmapBuffer is null)
        {
            return;
        }

        // Check if we need to (re)allocate buffers for this resolution
        if (frame.Width != _currentVideoWidth || frame.Height != _currentVideoHeight)
        {
            ReallocateBuffers(frame.Width, frame.Height);
        }

        if (_buffers.Count == 0)
        {
            _logger.LogWarning("No buffers available for rendering");
            return;
        }

        // Get next buffer
        var buffer = _buffers[_currentBufferIndex];
        _currentBufferIndex = (_currentBufferIndex + 1) % _buffers.Count;

        // Copy from MMAP buffer to DMA buffer
        var srcSpan = frame.MmapBuffer.MappedPlanes[0].AsSpan();
        var dstSpan = buffer.DmaBuffer.GetMappedSpan();
        srcSpan[..Math.Min(srcSpan.Length, dstSpan.Length)].CopyTo(dstSpan);

        buffer.DmaBuffer.SyncMap();
        _overlayPresenter.SetOverlayPlaneBuffer(buffer);
    }

    /// <summary>
    /// Renders a decoded FFmpeg frame to the overlay plane.
    /// Handles resolution changes automatically.
    /// </summary>
    private unsafe void RenderFfmpegFrame(FfmpegDecodedFrame frame)
    {
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

        // Copy frame data to DMA buffer
        CopyFfmpegFrame(avFrame, buffer);

        // Sync the buffer to ensure GPU/display can see the data
        buffer.DmaBuffer.SyncMap();

        // Present on overlay plane
        _overlayPresenter.SetOverlayPlaneBuffer(buffer);
    }

    private void ReallocateBuffers(uint width, uint height)
    {
        _logger.LogInformation(
            "Video resolution changed: {OldWidth}x{OldHeight} -> {NewWidth}x{NewHeight}, reallocating buffers",
            _currentVideoWidth, _currentVideoHeight, width, height);

        // Free existing buffers
        FreeBuffers();

        // Allocate new buffers with video resolution
        _logger.LogInformation("Allocating {Count} {Format} buffers for video ({Width}x{Height})",
            _bufferCount, _pixelFormat.GetName(), width, height);

        for (int i = 0; i < _bufferCount; i++)
        {
            var buffer = _bufferManager.AllocateBuffer(width, height, _pixelFormat);
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
    /// Copies FFmpeg frame data based on the pixel format.
    /// </summary>
    private unsafe void CopyFfmpegFrame(AVFrame* avFrame, SharedDmaBuffer buffer)
    {
        var dstSpan = buffer.DmaBuffer.GetMappedSpan();
        var dstStride = buffer.Stride;

        if (_pixelFormat.Fourcc == KnownPixelFormats.DRM_FORMAT_NV12.Fourcc)
        {
            CopyNv12(avFrame, dstSpan, dstStride, buffer.Height);
        }
        else if (_pixelFormat.Fourcc == KnownPixelFormats.DRM_FORMAT_YUV420.Fourcc)
        {
            CopyYuv420p(avFrame, dstSpan, dstStride, buffer.Height);
        }
        else
        {
            // For other formats, fall back to generic planar copy
            CopyPlanar(avFrame, dstSpan, dstStride, buffer.Height);
        }
    }

    /// <summary>
    /// Copy for NV12 format (semi-planar, Y + interleaved UV).
    /// </summary>
    private unsafe void CopyNv12(AVFrame* avFrame, Span<byte> dstSpan, uint dstStride, uint dstHeight)
    {
        var frameWidth = avFrame->width;
        var frameHeight = avFrame->height;

        fixed (byte* dstPtr = dstSpan)
        {
            // Copy Y plane
            var srcY = avFrame->data[0];
            var srcStrideY = avFrame->linesize[0];

            for (int y = 0; y < frameHeight; y++)
            {
                Buffer.MemoryCopy(
                    srcY + y * srcStrideY,
                    dstPtr + y * dstStride,
                    dstStride,
                    frameWidth);
            }

            // Copy UV plane
            var srcUv = avFrame->data[1];
            var srcStrideUv = avFrame->linesize[1];
            var uvPlaneOffset = dstStride * dstHeight;
            var uvHeight = frameHeight / 2;

            for (int y = 0; y < uvHeight; y++)
            {
                Buffer.MemoryCopy(
                    srcUv + y * srcStrideUv,
                    dstPtr + uvPlaneOffset + y * dstStride,
                    dstStride,
                    frameWidth);  // UV width equals Y width for NV12
            }
        }
    }

    /// <summary>
    /// Copy for YUV420P format (planar Y, U, V).
    /// Maps to DRM_FORMAT_YUV420 (I420).
    /// </summary>
    private unsafe void CopyYuv420p(AVFrame* avFrame, Span<byte> dstSpan, uint dstStride, uint dstHeight)
    {
        var frameWidth = avFrame->width;
        var frameHeight = avFrame->height;

        var srcY = avFrame->data[0];
        var srcU = avFrame->data[1];
        var srcV = avFrame->data[2];
        var srcStrideY = avFrame->linesize[0];
        var srcStrideU = avFrame->linesize[1];
        var srcStrideV = avFrame->linesize[2];

        // DRM_FORMAT_YUV420 (I420): Y plane, then U plane, then V plane
        var yPlaneSize = dstStride * dstHeight;
        var uvWidth = frameWidth / 2;
        var uvHeight = frameHeight / 2;
        var uvStride = dstStride / 2;
        var uvPlaneSize = uvStride * (dstHeight / 2);

        fixed (byte* dstPtr = dstSpan)
        {
            // Copy Y plane
            for (int y = 0; y < frameHeight; y++)
            {
                Buffer.MemoryCopy(
                    srcY + y * srcStrideY,
                    dstPtr + y * dstStride,
                    dstStride,
                    frameWidth);
            }

            // Copy U plane
            var uPlaneOffset = yPlaneSize;
            for (int y = 0; y < uvHeight; y++)
            {
                Buffer.MemoryCopy(
                    srcU + y * srcStrideU,
                    dstPtr + uPlaneOffset + y * uvStride,
                    uvStride,
                    uvWidth);
            }

            // Copy V plane
            var vPlaneOffset = yPlaneSize + uvPlaneSize;
            for (int y = 0; y < uvHeight; y++)
            {
                Buffer.MemoryCopy(
                    srcV + y * srcStrideV,
                    dstPtr + vPlaneOffset + y * uvStride,
                    uvStride,
                    uvWidth);
            }
        }
    }

    /// <summary>
    /// Generic planar copy for other formats.
    /// </summary>
    private unsafe void CopyPlanar(AVFrame* avFrame, Span<byte> dstSpan, uint dstStride, uint dstHeight)
    {
        var frameWidth = avFrame->width;
        var frameHeight = avFrame->height;

        fixed (byte* dstPtr = dstSpan)
        {
            // Copy first plane (Y)
            var srcY = avFrame->data[0];
            var srcStrideY = avFrame->linesize[0];

            for (int y = 0; y < frameHeight; y++)
            {
                Buffer.MemoryCopy(
                    srcY + y * srcStrideY,
                    dstPtr + y * dstStride,
                    dstStride,
                    frameWidth);
            }

            // Copy remaining planes if they exist
            var offset = dstStride * dstHeight;

            for (int plane = 1; plane < 4 && avFrame->data[(uint)plane] != null; plane++)
            {
                var srcPlane = avFrame->data[(uint)plane];
                var srcStride = avFrame->linesize[(uint)plane];
                var planeHeight = (plane > 0) ? frameHeight / 2 : frameHeight;
                var planeWidth = (plane > 0) ? frameWidth / 2 : frameWidth;
                var planeStride = dstStride / 2;

                for (int y = 0; y < planeHeight; y++)
                {
                    Buffer.MemoryCopy(
                        srcPlane + y * srcStride,
                        dstPtr + offset + y * planeStride,
                        planeStride,
                        planeWidth);
                }

                offset += planeStride * (dstHeight / 2);
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
