using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenHd.Ui.TestRx;

public class RemoteOpenHdConnector : IHostedService, IAsyncDisposable
{
    private readonly InMemoryPipeStreamAccessor _h264Stream;
    private readonly ILogger<RemoteOpenHdConnector> _logger;
    private readonly string _ipString;
    private readonly int _port;
    private readonly int _h264Port;
    private readonly CancellationTokenSource _stoppingCts = new();

    private Task? _tcpConnectionTask;
    private Task? _h264ListenTask;

    public RemoteOpenHdConnector(
        [FromKeyedServices("h264-stream")] InMemoryPipeStreamAccessor h264Stream,
        ILogger<RemoteOpenHdConnector> logger)
    {
        ArgumentNullException.ThrowIfNull(h264Stream);
        ArgumentNullException.ThrowIfNull(logger);

        _ipString = "192.168.88.160";
        _port = 5760;
        _h264Port = 5600;
        _h264Stream = h264Stream;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting RemoteOpenHdConnector");

        _tcpConnectionTask = RxThreadAsync(_stoppingCts.Token);
        _h264ListenTask = H264RxThreadAsync(_stoppingCts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping RemoteOpenHdConnector");

        try
        {
            await _stoppingCts.CancelAsync();
        }
        finally
        {
            var tasks = new[] { _tcpConnectionTask, _h264ListenTask }.Where(t => t != null).Cast<Task>().ToArray();
            if (tasks.Length > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("RemoteOpenHdConnector stopped");
    }

    private async Task RxThreadAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(_ipString, _port, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Connected to {IP}:{Port}", _ipString, _port);

                await using var stream = tcpClient.GetStream();
                var buffer = new byte[1024];

                while (!cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAtLeastAsync(buffer, 128, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        _logger.LogWarning("TCP connection closed by remote host");
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("TCP receiver cancelled");
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "TCP connection error to {IP}:{Port}. Retrying in 5 seconds", _ipString, _port);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TCP receiver. Retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("TCP receiver thread exited");
    }

    private async Task H264RxThreadAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var h264UdpServer = new UdpClient(new IPEndPoint(IPAddress.Any, _h264Port));
                _logger.LogInformation("H264 UDP receiver started on port {Port}", _h264Port);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await h264UdpServer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    _h264Stream.ProcessIncomingFrame(result.Buffer);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("H264 UDP receiver cancelled");
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "UDP socket error on port {Port}. Retrying in 5 seconds", _h264Port);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in H264 UDP receiver. Retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("H264 UDP receiver thread exited");
    }

    public async ValueTask DisposeAsync()
    {
        await _stoppingCts.CancelAsync();
        _stoppingCts.Dispose();
    }
}