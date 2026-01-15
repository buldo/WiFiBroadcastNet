using System.Runtime.Versioning;
using SharpVideo.Decoding.V4l2.Stateless;
using SharpVideo.Utils;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Manages video frame submission to the video overlay plane.
/// Provides a simple interface for the decoder to submit frames,
/// and handles frame lifecycle with the DualPlanePresenter.
/// </summary>
/// <remarks>
/// Thread Safety:
/// - SubmitFrame can be called from any thread (typically decoder thread)
/// - ProcessCompletedFrames should be called periodically from the OSD loop
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class VideoOverlayManager : IDisposable
{
    private readonly DualPlanePresenter _presenter;
    private readonly VideoFrameManager<V4l2H264StatelessDecoder, SharedDmaBuffer> _frameManager;
    private readonly Action<SharedDmaBuffer>? _onFrameSubmitted;
    private readonly ILogger _logger;

    private readonly Dictionary<SharedDmaBuffer, SharedDmaBuffer> _framesInFlight = new();
    private readonly object _lock = new();

    private int _submittedCount;
    private int _completedCount;
    private bool _disposed;

    /// <summary>
    /// Gets the total number of frames submitted to the overlay.
    /// </summary>
    public int SubmittedFrameCount => _submittedCount;

    /// <summary>
    /// Gets the total number of frames completed (returned to decoder).
    /// </summary>
    public int CompletedFrameCount => _completedCount;

    public VideoOverlayManager(
        DualPlanePresenter presenter,
        VideoFrameManager<V4l2H264StatelessDecoder, SharedDmaBuffer> frameManager,
        Action<SharedDmaBuffer>? onFrameSubmitted,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(frameManager);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _presenter = presenter;
        _frameManager = frameManager;
        _onFrameSubmitted = onFrameSubmitted;
        _logger = loggerFactory.CreateLogger<VideoOverlayManager>();

        _logger.LogInformation("VideoOverlayManager created");
    }

    /// <summary>
    /// Acquires the latest decoded frame and submits it to the video overlay.
    /// Call this from the OSD render loop to update the video plane.
    /// </summary>
    /// <returns>True if a new frame was submitted, false if no frame available</returns>
    public bool TrySubmitLatestFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var frame = _frameManager.AcquireCurrentFrame();
        if (frame == null)
        {
            return false;
        }

        SubmitFrame(frame);
        return true;
    }

    /// <summary>
    /// Submits a specific frame to the video overlay.
    /// </summary>
    public void SubmitFrame(SharedDmaBuffer buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);

        lock (_lock)
        {
            // Track frame for later release
            _framesInFlight[buffer] = buffer;
        }

        // Submit to presenter (zero-copy)
        _presenter.SubmitVideoFrame(buffer);

        _submittedCount++;
        _onFrameSubmitted?.Invoke(buffer);

        _logger.LogTrace("Submitted video frame #{Count}", _submittedCount);
    }

    /// <summary>
    /// Processes completed video buffers and returns frames to the decoder.
    /// Call this periodically from the OSD render loop.
    /// </summary>
    public void ProcessCompletedFrames()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var completedBuffers = _presenter.GetCompletedVideoBuffers();
        if (completedBuffers.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
            foreach (var buffer in completedBuffers)
            {
                if (_framesInFlight.Remove(buffer, out var frame))
                {
                    _frameManager.ReleaseFrame(frame);
                    _completedCount++;
                    _logger.LogTrace("Released video frame, in-flight: {Count}", _framesInFlight.Count);
                }
            }
        }
    }

    /// <summary>
    /// Releases all frames currently in flight.
    /// Call during cleanup.
    /// </summary>
    public void ReleaseAllFrames()
    {
        lock (_lock)
        {
            foreach (var frame in _framesInFlight.Values)
            {
                _frameManager.ReleaseFrame(frame);
                _completedCount++;
            }

            _framesInFlight.Clear();
            _logger.LogInformation("Released all in-flight frames");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // First, process any completed buffers from the presenter
        // This ensures buffers that were in-flight are properly returned
        try
        {
            var completedBuffers = _presenter.GetCompletedVideoBuffers();
            lock (_lock)
            {
                foreach (var buffer in completedBuffers)
                {
                    if (_framesInFlight.Remove(buffer, out var frame))
                    {
                        _frameManager.ReleaseFrame(frame);
                        _completedCount++;
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Presenter already disposed, that's fine
        }

        // Then release any remaining frames that weren't completed
        ReleaseAllFrames();

        _logger.LogInformation(
            "VideoOverlayManager disposed. Submitted: {Submitted}, Completed: {Completed}",
            _submittedCount, _completedCount);
    }
}
