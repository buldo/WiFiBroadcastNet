#nullable enable
using System.Buffers;
using System.Net.Sockets;
using System.Net;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.ObjectPool;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
internal class PooledUdpSource
{
    private readonly ILogger _logger;
    private readonly ArrayPool<byte> _receiveBuffersPool = ArrayPool<byte>.Shared;
    private readonly ObjectPool<RtpPacket> _packetsPool =
        new DefaultObjectPool<RtpPacket>(new DefaultPooledObjectPolicy<RtpPacket>(), 5);
    private readonly UdpClient _client;

    private Func<RtpPacket, Task>? _receiveHandler;
    private Task? _receiveTask;
    private CancellationTokenSource? _cts;

    public PooledUdpSource(
        IPEndPoint listenEndPoint,
        ILogger<PooledUdpSource> logger)
    {
        _logger = logger;
        _client = new UdpClient(listenEndPoint);
    }

    public void Start(Func<RtpPacket, Task> receiveHandler)
    {
        _receiveHandler = receiveHandler;
        _cts = new CancellationTokenSource();
        _receiveTask = Task.Run(async () => await ReceiveRoutine(_cts.Token), _cts.Token);
    }

    public async Task StopAsync()
    {
        if (_receiveTask != null)
        {
            _cts?.Cancel();
            await _receiveTask;
        }
    }

    public void ReusePacket(RtpPacket packet)
    {
        var buffer = packet.ReleaseBuffer();
        _receiveBuffersPool.Return(buffer);
        _packetsPool.Return(packet);
    }

    private async Task ReceiveRoutine(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var buffer = _receiveBuffersPool.Rent(Constants.MAX_UDP_SIZE);
            try
            {
                var read = await _client.Client.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                if (read > 0)
                {
                    var packet = _packetsPool.Get();
                    packet.ApplyBuffer(buffer, 0, read);
                    if (_receiveHandler != null)
                    {
                        await _receiveHandler(packet);
                    }
                }
                else
                {
                    _receiveBuffersPool.Return(buffer);
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
}
