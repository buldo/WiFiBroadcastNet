using Microsoft.Extensions.Logging;

namespace WiFiBroadcastNet.GroundHost;

public class UdpTransferAccessor : IStreamAccessor
{
    private readonly ILogger<UdpTransferAccessor> _logger;

    public UdpTransferAccessor(ILogger<UdpTransferAccessor> logger)
    {
        _logger = logger;
    }

    public void ProcessIncomingFrame(ReadOnlySpan<byte> payload)
    {
        _logger.LogWarning("Log payload");
    }
}