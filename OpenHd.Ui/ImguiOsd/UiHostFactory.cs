using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenHd.Ui.ImguiOsd;

internal class UiHostFactory
{
    private readonly InMemoryPipeStreamAccessor _h264Stream;
    private readonly ILoggerFactory _loggerFactory;

    public UiHostFactory(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        ILoggerFactory loggerFactory)
    {
        _h264Stream = h264Stream;
        _loggerFactory = loggerFactory;
    }

    public UiHostBase CreateHost()
    {
        if (OperatingSystem.IsWindows())
        {
            var logger = _loggerFactory.CreateLogger<WindowedHost>();
            return new WindowedHost(_h264Stream, logger);
        }

        var drmLogger = _loggerFactory.CreateLogger<DrmHost>();
        return new DrmHost(_h264Stream, drmLogger);
    }
}