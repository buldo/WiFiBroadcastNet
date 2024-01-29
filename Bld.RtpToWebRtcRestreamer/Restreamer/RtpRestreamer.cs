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
    private readonly TcpClient _tcpClient = new TcpClient();
    private readonly NetworkStream _stream;
    private readonly H264Depacketiser _h264Depacketiser = new();

    public RtpRestreamer(
        ILoggerFactory loggerFactory,
        IPEndPoint remoteIp)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<RtpRestreamer>();

        _tcpClient.Connect(remoteIp);
        _stream = _tcpClient.GetStream();
        var header = CreateConnectHeader();
        _stream.Write(header);
    }

    public void ApplyPacket(ReadOnlyMemory<byte> payload)
    {
        var packet = new RtpPacket();
        packet.ApplyBuffer(payload);
        var result = _h264Depacketiser.ProcessRtpPayload(
            packet.Payload.ToArray(),
            packet.Header.SequenceNumber,
            packet.Header.Timestamp,
            packet.Header.MarkerBit, out var isKeyframe);

        if (result != null)
        {
            var lenArray = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lenArray.AsSpan(), result.Length);
            _stream.Write(lenArray);
            _stream.Write(result);
        }
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