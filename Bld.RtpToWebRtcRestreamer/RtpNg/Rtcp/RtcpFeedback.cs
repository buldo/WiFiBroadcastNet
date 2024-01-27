//-----------------------------------------------------------------------------
// Filename: RtcpFeedback.cs
//
// Description:
//
//        RTCP Feedback Packet
//        0                   1                   2                   3
//        0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
// header |V=2|P|    RC   |   PT=SR=200   |             length            |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                  SSRC of packet sender                        |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                  SSRC of media source                         |
// info   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        :            Feedback Control Information(FCI)                  :
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//-----------------------------------------------------------------------------

using System.Buffers.Binary;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

internal class RtcpFeedback
{
    private static readonly ILogger Logger = Log.Logger;

    private readonly RtcpHeader _header;
    private readonly uint _mediaSsrc;
    private readonly ushort _pid; // Packet ID (PID): 16 bits to specify a lost packet, the RTP sequence number of the lost packet.
    private readonly ushort _blp; // bitmask of following lost packets (BLP): 16 bits
    private readonly int _senderPayloadSize = 20;

    /// <summary>
    /// Create a new RTCP Report from a serialised byte array.
    /// </summary>
    /// <param name="packet">The byte array holding the serialised feedback report.</param>
    public RtcpFeedback(ReadOnlySpan<byte> packet)
    {
        _header = new RtcpHeader(packet);

        var payloadIndex = RtcpHeader.HEADER_BYTES_LENGTH;
        SenderSsrc = BinaryPrimitives.ReadUInt32BigEndian(packet[payloadIndex..]);
        _mediaSsrc = BinaryPrimitives.ReadUInt32BigEndian(packet[(payloadIndex + 4)..]);

        switch (_header)
        {
            case var x when x.PacketType == RtcpReportTypes.RTPFB && x.FeedbackMessageType == RtcpFeedbackTypesEnum.RTCP_SR_REQ:
                _senderPayloadSize = 8;
                // PLI feedback reports do no have any additional parameters.
                break;
            case var x when x.PacketType == RtcpReportTypes.RTPFB:
                _senderPayloadSize = 12;
                _pid = BinaryPrimitives.ReadUInt16BigEndian(packet[(payloadIndex + 8)..]);
                _blp = BinaryPrimitives.ReadUInt16BigEndian(packet[(payloadIndex + 10)..]);
                break;

            case var x when x.PacketType == RtcpReportTypes.PSFB && x.PayloadFeedbackMessageType == PSFBFeedbackTypesEnum.PLI:
                _senderPayloadSize = 8;
                break;

            //default:
            //    throw new NotImplementedException($"Deserialisation for feedback report {Header.PacketType} not yet implemented.");
        }
    }

    public uint SenderSsrc { get; } // Packet Sender

    //0                   1                   2                   3
    //0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    //+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    //|V=2|P|   FMT   |       PT      |          length               |
    //+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    //|                  SSRC of packet sender                        |
    //+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    //|                  SSRC of media source                         |
    //+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    //:            Feedback Control Information(FCI)                  :
    //:                                                               :
    public byte[] GetBytes()
    {
        var buffer = new byte[RtcpHeader.HEADER_BYTES_LENGTH + _senderPayloadSize];
        _header.SetLength((ushort)(buffer.Length / 4 - 1));

        Buffer.BlockCopy(_header.GetBytes(), 0, buffer, 0, RtcpHeader.HEADER_BYTES_LENGTH);
        var payloadIndex = RtcpHeader.HEADER_BYTES_LENGTH;

        // All feedback packets require the Sender and Media SSRC's to be set.
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(payloadIndex, 4), SenderSsrc);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(payloadIndex + 4, 4), _mediaSsrc);

        switch (_header)
        {
            case var x when x.PacketType == RtcpReportTypes.RTPFB && x.FeedbackMessageType == RtcpFeedbackTypesEnum.RTCP_SR_REQ:
                // PLI feedback reports do no have any additional parameters.
                break;
            case var x when x.PacketType == RtcpReportTypes.RTPFB:
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(payloadIndex + 8, 2), _pid);
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(payloadIndex + 10, 2), _blp);
                break;

            case var x when x.PacketType == RtcpReportTypes.PSFB && x.PayloadFeedbackMessageType == PSFBFeedbackTypesEnum.PLI:
                break;
            case var x when x.PacketType == RtcpReportTypes.PSFB && x.PayloadFeedbackMessageType == PSFBFeedbackTypesEnum.AFB:
                // Application feedback reports do no have any additional parameters?
                break;
            default:
                Logger?.LogDebug($"Serialization for feedback report {_header.PacketType} and message type "
                                 + $"{_header.FeedbackMessageType} not yet implemented.");
                break;
            //throw new NotImplementedException($"Serialisation for feedback report {Header.PacketType} and message type "
            //+ $"{Header.FeedbackMessageType} not yet implemented.");
        }
        return buffer;
    }
}