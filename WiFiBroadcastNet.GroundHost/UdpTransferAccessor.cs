using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace WiFiBroadcastNet.GroundHost;

public class UdpTransferAccessor : IStreamAccessor
{
    private readonly ILogger<UdpTransferAccessor> _logger;
    private readonly IPEndPoint? _endPoint;
    private readonly UdpClient _udpClient = new();

    public UdpTransferAccessor(
        ILogger<UdpTransferAccessor> logger,
        IPEndPoint? endPoint)
    {
        _logger = logger;
        _endPoint = endPoint;
    }

    public void ProcessIncomingFrame(ReadOnlyMemory<byte> payload)
    {
        if (_endPoint != null)
        {
            _udpClient.Send(payload.Span, _endPoint);
        }
    }
}