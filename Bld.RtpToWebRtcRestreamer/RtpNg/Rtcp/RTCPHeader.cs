//-----------------------------------------------------------------------------
// Notes:
//
//      RTCP Header
//        0                   1                   2                   3
//        0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//       +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//header |V=2|P|    RC   |   PT=SR=200   |             Length            |
//       +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//       |                         Payload                               |
//       +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// (V)ersion (2 bits) = 2
// (P)adding (1 bit) = Indicates whether the packet contains additional padding octets.
// Reception Report Count (RC) (5 bits) = The number of reception report blocks contained in this packet. A
//      value of zero is valid.
// Packet Type (PT) (8 bits) = Contains the constant 200 to identify this as an RTCP SR packet.
// Length (16 bits) = The length of this RTCP packet in 32-bit words minus one, including the header and any padding.
//-----------------------------------------------------------------------------

using System.Buffers.Binary;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// RTCP Header as defined in RFC3550.
/// </summary>
internal class RtcpHeader
{
    public const int HEADER_BYTES_LENGTH = 4;

    private int Version { get; }
    private int PaddingFlag { get; } // 1 bit.
    public int ReceptionReportCount { get; } // 5 bits.
    public RtcpReportTypes PacketType { get; }       // 8 bits.
    private ushort Length { get; set; }                        // 16 bits.

    /// <summary>
    /// The Feedback Message Type is used for RFC4585 transport layer feedback reports.
    /// When used this field gets set in place of the Reception Report Counter field.
    /// </summary>
    public RtcpFeedbackTypesEnum FeedbackMessageType { get; } = RtcpFeedbackTypesEnum.unassigned;

    /// <summary>
    /// The Payload Feedback Message Type is used for RFC4585 payload layer feedback reports.
    /// When used this field gets set in place of the Reception Report Counter field.
    /// </summary>
    public PSFBFeedbackTypesEnum PayloadFeedbackMessageType { get; } = PSFBFeedbackTypesEnum.unassigned;

    /// <summary>
    /// Identifies whether an RTCP header is for a standard RTCP packet or for an
    /// RTCP feedback report.
    /// </summary>
    /// <returns>True if the header is for an RTCP feedback report or false if not.</returns>
    private bool IsFeedbackReport()
    {
        if (PacketType == RtcpReportTypes.RTPFB ||
            PacketType == RtcpReportTypes.PSFB)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extract and load the RTCP header from an RTCP packet.
    /// </summary>
    public RtcpHeader(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < HEADER_BYTES_LENGTH)
        {
            throw new ApplicationException("The packet did not contain the minimum number of bytes for an RTCP header packet.");
        }

        var firstWord = BinaryPrimitives.ReadUInt16BigEndian(packet);

        Length = BinaryPrimitives.ReadUInt16BigEndian(packet[2..]);

        Version = Convert.ToInt32(firstWord >> 14);
        PaddingFlag = Convert.ToInt32((firstWord >> 13) & 0x1);
        PacketType = (RtcpReportTypes)(firstWord & 0x00ff);

        if (IsFeedbackReport())
        {
            if (PacketType == RtcpReportTypes.RTPFB)
            {
                FeedbackMessageType = (RtcpFeedbackTypesEnum)((firstWord >> 8) & 0x1f);
            }
            else
            {
                PayloadFeedbackMessageType = (PSFBFeedbackTypesEnum)((firstWord >> 8) & 0x1f);
            }
        }
        else
        {
            ReceptionReportCount = Convert.ToInt32((firstWord >> 8) & 0x1f);
        }
    }

    /// <summary>
    /// The length of this RTCP packet in 32-bit words minus one,
    /// including the header and any padding.
    /// </summary>
    public void SetLength(ushort length)
    {
        Length = length;
    }

    public byte[] GetBytes()
    {
        var header = new byte[4];

        var firstWord = ((uint)Version << 30) + ((uint)PaddingFlag << 29) + ((uint)PacketType << 16) + Length;

        if (IsFeedbackReport())
        {
            if (PacketType == RtcpReportTypes.RTPFB)
            {
                firstWord += (uint)FeedbackMessageType << 24;
            }
            else
            {
                firstWord += (uint)PayloadFeedbackMessageType << 24;
            }
        }
        else
        {
            firstWord += (uint)ReceptionReportCount << 24;
        }

        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), firstWord);

        return header;
    }
}