using System.Runtime.Versioning;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using SharpVideo.Decoding;
using SharpVideo.DmaBuffers;
using SharpVideo.Drm;
using SharpVideo.Linux.Native;
using SharpVideo.Utils;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Manages video frame output to DRM overlay plane using DMA buffers.
/// Supports multiple pixel formats with zero-copy rendering when formats match,
/// or automatic conversion when needed.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class VideoPlaneRenderer : IDisposable
{
    private readonly DrmPlaneLastDmaBufferPresenter _overlayPresenter;
    private readonly DrmBufferManager _bufferManager;
    private readonly ILogger<VideoPlaneRenderer> _logger;
    private readonly int _bufferCount;
    private readonly PixelFormat _sourceFormat;
    private readonly PixelFormat _targetDrmFormat;
    private readonly bool _requiresConversion;

    private readonly List<SharedDmaBuffer> _buffers = [];
    private int _currentBufferIndex;
    private uint _currentVideoWidth;
    private uint _currentVideoHeight;
    private bool _disposed;

    /// <summary>
    /// Creates a new VideoPlaneRenderer with specified source and target formats.
    /// </summary>
    /// <param name="overlayPresenter">DRM overlay plane presenter</param>
    /// <param name="bufferManager">Buffer manager for DMA buffer allocation</param>
    /// <param name="sourceFormat">Decoder pixel format</param>
    /// <param name="targetDrmFormat">DRM pixel format for display</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="bufferCount">Number of buffers to allocate</param>
    public VideoPlaneRenderer(
        DrmPlaneLastDmaBufferPresenter overlayPresenter,
        DrmBufferManager bufferManager,
        PixelFormat sourceFormat,
        PixelFormat targetDrmFormat,
        ILogger<VideoPlaneRenderer> logger,
        int bufferCount = 3)
    {
        _overlayPresenter = overlayPresenter ?? throw new ArgumentNullException(nameof(overlayPresenter));
        _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bufferCount = bufferCount;
        _sourceFormat = sourceFormat;
        _targetDrmFormat = targetDrmFormat;
        _requiresConversion = PixelFormatConverter.RequiresConversion(sourceFormat, targetDrmFormat);

        var sourceFormatName = PixelFormatConverter.GetFormatName(sourceFormat);
        var targetFormatName = targetDrmFormat.GetName();

        if (_requiresConversion)
        {
            _logger.LogInformation(
                "VideoPlaneRenderer created: {SourceFormat} -> {TargetFormat} (conversion required)",
                sourceFormatName, targetFormatName);
        }
        else
        {
            _logger.LogInformation(
                "VideoPlaneRenderer created: {SourceFormat} -> {TargetFormat} (zero-copy)",
                sourceFormatName, targetFormatName);
        }
    }

    /// <summary>
    /// Renders a decoded FFmpeg frame to the overlay plane.
    /// Uses zero-copy when formats match, or converts when necessary.
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

        // Copy frame data to DMA buffer (with or without conversion)
        if (_requiresConversion)
        {
            CopyFrameWithConversion(avFrame, buffer);
        }
        else
        {
            CopyFrameDirect(avFrame, buffer);
        }

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

        // Allocate new buffers with video resolution using target format
        _logger.LogInformation("Allocating {Count} {Format} buffers for video ({Width}x{Height})",
            _bufferCount, _targetDrmFormat.GetName(), width, height);

        for (int i = 0; i < _bufferCount; i++)
        {
            var buffer = _bufferManager.AllocateBuffer(width, height, _targetDrmFormat);
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
    /// Copies frame data directly when formats match (zero-copy path).
    /// </summary>
    private unsafe void CopyFrameDirect(AVFrame* avFrame, SharedDmaBuffer buffer)
    {
        var dstSpan = buffer.DmaBuffer.GetMappedSpan();
        var dstStride = buffer.Stride;

        // Handle based on source format
        if (_sourceFormat.Fourcc == KnownPixelFormats.DRM_FORMAT_NV12.Fourcc)
        {
            CopyNv12Direct(avFrame, dstSpan, dstStride, buffer.Height);
        }
        else if (_sourceFormat.Fourcc == KnownPixelFormats.DRM_FORMAT_YUV420.Fourcc)
        {
            CopyYuv420pDirect(avFrame, dstSpan, dstStride, buffer.Height);
        }
        else
        {
            // For other formats, fall back to generic planar copy
            CopyPlanarDirect(avFrame, dstSpan, dstStride, buffer.Height);
        }
    }

    /// <summary>
    /// Direct copy for NV12 format (semi-planar, Y + interleaved UV).
    /// </summary>
    private unsafe void CopyNv12Direct(AVFrame* avFrame, Span<byte> dstSpan, uint dstStride, uint dstHeight)
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
    /// Direct copy for YUV420P format (planar Y, U, V).
    /// Maps to DRM_FORMAT_YUV420 (I420).
    /// </summary>
    private unsafe void CopyYuv420pDirect(AVFrame* avFrame, Span<byte> dstSpan, uint dstStride, uint dstHeight)
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
        // Strides may differ between src and dst
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
    private unsafe void CopyPlanarDirect(AVFrame* avFrame, Span<byte> dstSpan, uint dstStride, uint dstHeight)
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

    /// <summary>
    /// Copies frame with format conversion (e.g., YUV420P -> NV12).
    /// </summary>
    private unsafe void CopyFrameWithConversion(AVFrame* avFrame, SharedDmaBuffer buffer)
    {
        // Currently only YUV420P -> NV12 conversion is implemented
        if (_sourceFormat.Fourcc == KnownPixelFormats.DRM_FORMAT_YUV420.Fourcc &&
            _targetDrmFormat.Fourcc == KnownPixelFormats.DRM_FORMAT_NV12.Fourcc)
        {
            ConvertYuv420pToNv12(avFrame, buffer);
        }
        else
        {
            _logger.LogWarning(
                "Unsupported conversion: {Source} -> {Target}",
                PixelFormatConverter.GetFormatName(_sourceFormat),
                _targetDrmFormat.GetName());
        }
    }

    /// <summary>
    /// Converts YUV420P (planar) to NV12 (semi-planar) format.
    /// YUV420P: Y plane, U plane, V plane (all separate)
    /// NV12: Y plane, UV interleaved plane
    /// </summary>
    private unsafe void ConvertYuv420pToNv12(AVFrame* avFrame, SharedDmaBuffer buffer)
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
