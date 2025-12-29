using Microsoft.Extensions.Logging;
using SharpVideo.Decoding;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// Manages video frame synchronization between decoder and renderer threads.
/// Provides thread-safe access to decoded frames with automatic frame dropping.
/// </summary>
internal sealed class VideoFrameManager : IDisposable
{
    private readonly BaseDecoder _decoder;
    private readonly ILogger<VideoFrameManager> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly object _frameLock = new();
    
    private FfmpegDecodedFrame? _currentFrame;
    private Task? _frameFetchThread;
    private bool _disposed;

    public VideoFrameManager(BaseDecoder decoder, ILogger<VideoFrameManager> logger)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the frame fetching loop on a background thread.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (_frameFetchThread != null)
        {
            throw new InvalidOperationException("VideoFrameManager is already started");
        }

        _logger.LogInformation("Starting frame fetching loop");
        _frameFetchThread = Task.Factory.StartNew(
            FrameFetchingLoop, 
            TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Stops the frame fetching loop and releases any pending frames.
    /// </summary>
    public async Task StopAsync()
    {
        if (_disposed) return;

        _logger.LogInformation("Stopping VideoFrameManager");
        await _cancellationTokenSource.CancelAsync();

        if (_frameFetchThread != null)
        {
            await _frameFetchThread;
            _frameFetchThread = null;
        }

        lock (_frameLock)
        {
            if (_currentFrame != null)
            {
                _decoder.ReuseDecodedFrame(_currentFrame);
                _currentFrame = null;
            }
        }
    }

    /// <summary>
    /// Attempts to acquire the current frame for rendering.
    /// Returns null if no frame is available.
    /// The caller is responsible for calling ReleaseFrame() after rendering.
    /// </summary>
    public FfmpegDecodedFrame? AcquireCurrentFrame()
    {
        lock (_frameLock)
        {
            var frame = _currentFrame;
            _currentFrame = null;
            return frame;
        }
    }

    /// <summary>
    /// Releases a frame back to the decoder for reuse.
    /// </summary>
    public void ReleaseFrame(FfmpegDecodedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        
        _logger.LogTrace("Returning frame to decoder");
        _decoder.ReuseDecodedFrame(frame);
        _logger.LogTrace("Frame returned to decoder");
    }

    private void FrameFetchingLoop()
    {
        _logger.LogInformation("Starting frame fetching loop");
        int frameCount = 0;

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.LogTrace("Waiting for decoded frame #{Count}", frameCount + 1);

                var decodedFrame = _decoder.WaitForDecodedFrames();

                _logger.LogTrace("Received decoded frame #{Count}", frameCount + 1);

                if (decodedFrame is FfmpegDecodedFrame ffmpegFrame)
                {
                    FfmpegDecodedFrame? frameToReturn = null;

                    lock (_frameLock)
                    {
                        if (_currentFrame != null)
                        {
                            _logger.LogWarning(
                                "Overwriting unrendered frame! Frame #{Count} - returning old frame", 
                                frameCount);
                            frameToReturn = _currentFrame;
                        }
                        _currentFrame = ffmpegFrame;
                        _logger.LogTrace("Frame #{Count} ready for rendering", frameCount + 1);
                    }

                    if (frameToReturn != null)
                    {
                        _logger.LogTrace("Returning overwritten frame to decoder");
                        _decoder.ReuseDecodedFrame(frameToReturn);
                    }

                    frameCount++;

                    if (frameCount % 30 == 0)
                    {
                        _logger.LogDebug("Fetched {Count} frames so far", frameCount);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in frame fetching loop after {Count} frames", frameCount);
        }
        finally
        {
            _logger.LogInformation("Exiting frame fetching loop, fetched {Count} frames", frameCount);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        
        lock (_frameLock)
        {
            if (_currentFrame != null)
            {
                _decoder.ReuseDecodedFrame(_currentFrame);
                _currentFrame = null;
            }
        }

        _disposed = true;
    }
}
