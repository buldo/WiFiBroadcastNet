using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenHd.Ui.TestRx;

public class RemoteOpenHdConnector : IHostedService
{
    private readonly InMemoryPipeStreamAccessor _h264Stream;
    private readonly ILogger<RemoteOpenHdConnector> _logger;
    private readonly string _ipString;
    private readonly int _port;
    private Task _tcpConnectionTask;
    private Task _h264ListenTask;

    public RemoteOpenHdConnector(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        ILogger<RemoteOpenHdConnector> logger)
    {
        _ipString = "192.168.88.160";
        _port = 5760;
        _h264Stream = h264Stream;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _tcpConnectionTask = Task.Factory.StartNew(RxThread, TaskCreationOptions.LongRunning);
        _h264ListenTask = Task.Factory.StartNew(H264RxThread, TaskCreationOptions.LongRunning);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void RxThread()
    {
        using var tcpClient = new TcpClient();
        tcpClient.Connect(_ipString, _port);
        _logger.LogInformation("Connected to {IP}:{Port}", _ipString, _port);
        using var stream = tcpClient.GetStream();
        var buffer = new byte[1024];
        while (true)
        {
            stream.ReadAtLeast(buffer, 128);
            //_logger.LogInformation("Rx via TCP");
        }
    }

    private void H264RxThread()
    {
        using var h264UdpServer = new UdpClient(new IPEndPoint(IPAddress.Any, 5600));
        while (true)
        {
            IPEndPoint ip = default;
            var data = h264UdpServer.Receive(ref ip);
            //_logger.LogInformation("Rx via UDP");
            _h264Stream.ProcessIncomingFrame(data);
        }
    }
}