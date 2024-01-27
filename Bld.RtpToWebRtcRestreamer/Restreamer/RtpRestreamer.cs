using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.Restreamer;

public class RtpRestreamer
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RtpRestreamer> _logger;
    private readonly PooledUdpSource _receiver;
    private readonly TcpClient _tcpClient = new TcpClient();
    private readonly NetworkStream _stream;

    public RtpRestreamer(
        ILoggerFactory loggerFactory,
        IPEndPoint remoteIp)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<RtpRestreamer>();

        _receiver = new PooledUdpSource(_loggerFactory.CreateLogger<PooledUdpSource>());
        _receiver.Start(RtpProcessorAsync);

        _tcpClient.Connect(remoteIp);
        _stream = _tcpClient.GetStream();
        var header = CreateConnectHeader();
        _stream.Write(header);
    }

    private void RtpProcessorAsync(RtpPacket packet)
    {
        var lenArray = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenArray.AsSpan(), packet.Payload.Length);
        _stream.Write(lenArray);
        _stream.Write(packet.Payload);

        _receiver.ReusePacket(packet);
    }

    public void ApplyPacket(ReadOnlyMemory<byte> payload)
    {
        _receiver.ReceiveRoutine(payload);
    }

    private byte[] CreateConnectHeader()
    {
        var header = new byte[4*4];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0,4), 0x00042069);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(3,4), 1280);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(7, 4), 720);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(11, 4), 60);

        return header;
    }
}