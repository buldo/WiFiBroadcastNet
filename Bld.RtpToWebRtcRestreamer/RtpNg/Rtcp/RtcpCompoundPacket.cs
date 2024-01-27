using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

internal class RtcpCompoundPacket
{
    private static readonly ILogger Logger = Log.Logger;

    public RtcpBye Bye { get; }

    /// <summary>
    /// Creates a new RTCP compound packet from a serialised buffer.
    /// </summary>
    /// <param name="packet">The serialised RTCP compound packet to parse.</param>
    public RtcpCompoundPacket(ReadOnlySpan<byte> packet)
    {
        var offset = 0;
        while (offset < packet.Length)
        {
            if (packet.Length - offset < RtcpHeader.HEADER_BYTES_LENGTH)
            {
                // Not enough bytes left for a RTCP header.
                break;
            }

            var buffer = packet.Slice(offset);

            // The payload type field is the second byte in the RTCP header.
            var packetTypeId = buffer[1];
            RtcpFeedback feedback;
            switch (packetTypeId)
            {
                case (byte)RtcpReportTypes.SR:
                    var senderReport = new RtcpSenderReport(buffer);
                    var srLength = senderReport.GetBytes().Length;
                    offset += srLength;
                    break;
                case (byte)RtcpReportTypes.RR:
                    var receiverReport = new RtcpReceiverReport(buffer);
                    var rrLength = receiverReport.GetBytes().Length;
                    offset += rrLength;
                    break;
                case (byte)RtcpReportTypes.SDES:
                    var sDesReport = new RtcpSDesReport(buffer);
                    var sdesLength = sDesReport.GetBytes().Length;
                    offset += sdesLength;
                    break;
                case (byte)RtcpReportTypes.BYE:
                    Bye = new RtcpBye(buffer);
                    var byeLength = Bye != null ? Bye.GetBytes().Length : int.MaxValue;
                    offset += byeLength;
                    break;
                case (byte)RtcpReportTypes.RTPFB:
                    // TODO: Interpret Generic RTP feedback reports.
                    feedback = new RtcpFeedback(buffer);
                    var rtpfbFeedbackLength = feedback.GetBytes().Length;
                    offset += rtpfbFeedbackLength;
                    //var rtpfbHeader = new RtcpHeader(buffer);
                    //offset += rtpfbHeader.Length * 4 + 4;
                    break;
                case (byte)RtcpReportTypes.PSFB:
                    // TODO: Interpret Payload specific feedback reports.
                    feedback = new RtcpFeedback(buffer);
                    var psfbFeedbackLength = feedback.GetBytes().Length;
                    offset += psfbFeedbackLength;
                    //var psfbHeader = new RtcpHeader(buffer);
                    //offset += psfbHeader.Length * 4 + 4;
                    break;
                default:
                    Logger.LogWarning($"RTCPCompoundPacket did not recognise packet type ID {packetTypeId}.");
                    offset = int.MaxValue;
                    Logger.LogWarning(packet.HexStr());
                    break;
            }
        }
    }
}