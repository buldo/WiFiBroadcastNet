using System.Net;

using Bld.RtpToWebRtcRestreamer.Restreamer;

using Microsoft.Extensions.Logging;
using WiFiBroadcastNet;

namespace WtfDemo.Services;

public class WtfTransferAccessor : IStreamAccessor
{
    private readonly RtpRestreamer _restreamer;

    public WtfTransferAccessor(
        ILoggerFactory loggerFactory,
        IPEndPoint? endPoint)
    {
        _restreamer = new RtpRestreamer(loggerFactory, endPoint);
    }

    public void ProcessIncomingFrame(ReadOnlyMemory<byte> payload)
    {
        _restreamer.ApplyPacket(payload);
    }
}