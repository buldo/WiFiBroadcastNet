using FFmpeg.AutoGen;

using Microsoft.Extensions.Logging;

using SharpVideo.Decoding;
using SharpVideo.Decoding.Ffmpeg;
using SharpVideo.FfmpegBin;

namespace OpenHd.Ui;

public class DecodersFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DecodersFactory> _logger;

    private bool _ffmpegLoaded;

    public DecodersFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<DecodersFactory>();
    }

    public BaseDecoder CreateH264Decoder()
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

        return FfmpegH264Decoder.Create(_loggerFactory);
    }
}