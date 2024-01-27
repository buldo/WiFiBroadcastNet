#nullable enable
using System.Buffers;
using Microsoft.Extensions.Logging;

using Microsoft.Extensions.ObjectPool;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
internal class PooledUdpSource
{
    private readonly ILogger _logger;
    private readonly ArrayPool<byte> _receiveBuffersPool = ArrayPool<byte>.Shared;
    private readonly ObjectPool<RtpPacket> _packetsPool =
        new DefaultObjectPool<RtpPacket>(new DefaultPooledObjectPolicy<RtpPacket>(), 5);

    private Action<RtpPacket>? _receiveHandler;

    public PooledUdpSource(
        ILogger<PooledUdpSource> logger)
    {
        _logger = logger;
    }

    public void Start(Action<RtpPacket> receiveHandler)
    {
        _receiveHandler = receiveHandler;
    }

    public void ReusePacket(RtpPacket packet)
    {
        var buffer = packet.ReleaseBuffer();
        _receiveBuffersPool.Return(buffer);
        _packetsPool.Return(packet);
    }

    public void ReceiveRoutine(ReadOnlyMemory<byte> packetData)
    {
        var buffer = _receiveBuffersPool.Rent(Constants.MAX_UDP_SIZE);
        try
        {
            packetData.CopyTo(buffer);
            var packet = _packetsPool.Get();
            packet.ApplyBuffer(buffer, 0, packetData.Length);
            if (_receiveHandler != null)
            {
                _receiveHandler(packet);
            }
        }
        catch (Exception exception)
        {
            _receiveBuffersPool.Return(buffer);
            _logger.LogError(exception, "Error");
            throw;
        }

    }
}
