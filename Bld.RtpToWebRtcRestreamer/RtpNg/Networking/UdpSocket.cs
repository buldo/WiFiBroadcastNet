#nullable enable

using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Networking;

internal class UdpSocket
{
    private readonly UdpClient _client;
    private readonly ILogger<UdpSocket> _logger;
    private readonly CancellationTokenSource _cts = new();

    private bool _isStarted;
    private Func<UdpReceiveResult, Task>? _receiveAction;
    private Task? _receiveTask;

    public UdpSocket(UdpClient client, ILogger<UdpSocket> logger)
    {
        _client = client;
        _logger = logger;
    }

    public IPEndPoint LocalEndpoint => (IPEndPoint)_client.Client.LocalEndPoint;

    public void StartReceive(Func<UdpReceiveResult, Task> receiveAction)
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;

        _receiveAction = receiveAction;
        _receiveTask = Task.Run(async () => await ReceiveRoutine(_cts.Token));
    }

    private async Task ReceiveRoutine(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _client.ReceiveAsync(cancellationToken);
                await _receiveAction(result);
            }
            catch (Exception exception)
            {

            }
        }
    }

    public async ValueTask SendToAsync(ReadOnlyMemory<byte> buffer, IPEndPoint dstEndPoint)
    {
        if (!_isStarted)
        {
            return;
        }

        if (buffer.Length == 0)
        {
            return;
        }

        if (IPAddress.Any.Equals(dstEndPoint.Address) || IPAddress.IPv6Any.Equals(dstEndPoint.Address))
        {
            _logger.LogWarning($"The destination address for Send in RTPChannel cannot be {dstEndPoint.Address}.");
            return;
        }

        try
        {
            await _client.Client.SendToAsync(buffer, SocketFlags.None, dstEndPoint);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception RTPChannel.Send.");
        }
    }

    public async Task StopAsync()
    {
        if (!_isStarted)
        {
            return;
        }

        _isStarted = false;

        _cts.Cancel();
        if (_receiveTask != null)
        {
            await _receiveTask;
        }
    }
}