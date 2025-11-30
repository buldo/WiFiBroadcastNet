using Microsoft.Extensions.Logging;
using WiFiBroadcastNet;

namespace OpenHd.Ui;

public class DummyStreamAccessor : IStreamAccessor
{
    private readonly ILogger<DummyStreamAccessor> _logger;

    private long _counter = 0;

    public DummyStreamAccessor(ILogger<DummyStreamAccessor> logger)
    {
        _logger = logger;
    }

    public void ProcessIncomingFrame(ReadOnlyMemory<byte> payload)
    {
        _counter++;
        if (_counter % 1000 == 0)
        {
            _logger.LogInformation("Received {Count} frames", _counter);
        }
    }
}
