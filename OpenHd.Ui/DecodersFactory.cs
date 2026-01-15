using System.Runtime.Versioning;
using FFmpeg.AutoGen;
using SharpVideo.Decoding;
using SharpVideo.Decoding.Ffmpeg;
using SharpVideo.Decoding.V4l2.Discovery;
using SharpVideo.Decoding.V4l2.Stateful;
using SharpVideo.Decoding.V4l2.Stateless;
using SharpVideo.FfmpegBin;
using SharpVideo.Utils;
using SharpVideo.V4L2;

namespace OpenHd.Ui;

/// <summary>
/// Factory for creating H264 decoders with automatic hardware detection.
/// </summary>
public class DecodersFactory
{
    private readonly V4l2H264DecoderProvider _v4L2Provider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DecodersFactory> _logger;

    private bool _ffmpegLoaded;

    public DecodersFactory(
        V4l2H264DecoderProvider v4L2Provider,
        ILoggerFactory loggerFactory)
    {
        _v4L2Provider = v4L2Provider;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<DecodersFactory>();
    }

    /// <summary>
    /// Creates an FFmpeg-based H264 decoder.
    /// </summary>
    /// <returns>An FFmpeg H264 decoder instance.</returns>
    public FfmpegH264Decoder CreateFfmpegDecoder()
    {
        EnsureFfmpegLoaded();
        var decoder = FfmpegH264Decoder.Create(_loggerFactory);
        decoder.Initialize();
        return decoder;
    }

    /// <summary>
    /// Attempts to create a V4L2 hardware decoder with the provided buffer manager.
    /// </summary>
    /// <param name="drmBufferManager">DRM buffer manager for zero-copy decoding.</param>
    /// <returns>A V4L2 decoder if available, null otherwise.</returns>
    [SupportedOSPlatform("linux")]
    public IDecoder CreateV4l2Decoder(DrmBufferManager drmBufferManager)
    {
        ArgumentNullException.ThrowIfNull(drmBufferManager);

        var decoderInfo = _v4L2Provider.FindBestDecoder();
        if (decoderInfo == null)
        {
            throw new Exception("Failed to find decoder");
        }

        var device = V4L2DeviceFactory.Open(decoderInfo.DevicePath);
        if (device == null)
        {
            throw new Exception($"Failed to open device {decoderInfo.DevicePath}");
        }

        IDecoder? decoder;

        if (decoderInfo.DecoderType == V4l2H264DecoderType.Stateful)
        {
            decoder = V4l2H264StatefulDecoder.Create(
                device,
                _loggerFactory,
                null,
                drmBufferManager);
        }
        else if(decoderInfo.DecoderType == V4l2H264DecoderType.Stateless)
        {
            var mediaDevice = MediaDevice.Open(decoderInfo.MediaDevicePath!);

            decoder = V4l2H264StatelessDecoder.Create(
                device,
                mediaDevice,
                _loggerFactory,
                null,
                drmBufferManager);
        }
        else
        {
            decoder = null;
        }

        if (decoder == null)
        {
            throw new Exception("Failed to create decoder");
        }

        decoder.Initialize();

        return decoder;
    }

    private void EnsureFfmpegLoaded()
    {
        if (!_ffmpegLoaded)
        {
            var ffmpegPath = FfmpegLoader.Load(_logger);
            if (ffmpegPath != null)
            {
                ffmpeg.RootPath = ffmpegPath;
            }

            _ffmpegLoaded = true;
        }
    }
}