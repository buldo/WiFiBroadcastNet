namespace OpenHd.Ui.ImguiOsd;

internal class UiHostFactory
{
    private readonly InMemoryPipeStreamAccessor _h264Stream;
    private readonly DecodersFactory _decodersFactory;
    private readonly ILoggerFactory _loggerFactory;

    public UiHostFactory(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        DecodersFactory decodersFactory,
        ILoggerFactory loggerFactory)
    {
        _h264Stream = h264Stream;
        _decodersFactory = decodersFactory;
        _loggerFactory = loggerFactory;
    }

    public UiHostBase CreateHost()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowedHost(
                _h264Stream,
                _decodersFactory,
                _loggerFactory,
                _loggerFactory.CreateLogger<WindowedHost>());
        }

        return new DrmHost(
            _h264Stream,
            _decodersFactory,
            _loggerFactory.CreateLogger<DrmHost>());
    }
}