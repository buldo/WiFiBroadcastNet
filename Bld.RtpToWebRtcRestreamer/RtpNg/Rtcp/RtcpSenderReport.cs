//-----------------------------------------------------------------------------
// Filename: RtcpSenderReport.cs
//
// Description:
//
//        RTCP Sender Report Packet
//        0                   1                   2                   3
//        0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
// header |V=2|P|    RC   |   PT=SR=200   |             length            |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                         SSRC of sender                        |
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
// sender |              NTP timestamp, most significant word             |
// info   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |             NTP timestamp, least significant word             |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                         RTP timestamp                         |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                     sender's packet count                     |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                      sender's octet count                     |
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
// report |                 SSRC_1(SSRC of first source)                  |
// block  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//  1     | fraction lost |       cumulative number of packets lost       |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |           extended highest sequence number received           |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                      interarrival jitter                      |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                         last SR(LSR)                          |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                   delay since last SR(DLSR)                   |
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 12 Aug 2019  Aaron Clauson   Created, Montreux, Switzerland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Buffers.Binary;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// An RTCP sender report is for use by active RTP senders.
/// </summary>
/// <remarks>
/// From https://tools.ietf.org/html/rfc3550#section-6.4:
/// "The only difference between the
/// sender report(SR) and receiver report(RR) forms, besides the packet
/// type code, is that the sender report includes a 20-byte sender
/// information section for use by active senders.The SR is issued if a
/// site has sent any data packets during the interval since issuing the
/// last report or the previous one, otherwise the RR is issued."
/// </remarks>
internal class RtcpSenderReport
{
    private const int SENDER_PAYLOAD_SIZE = 20;
    private const int MIN_PACKET_SIZE = RtcpHeader.HEADER_BYTES_LENGTH + 4 + SENDER_PAYLOAD_SIZE;

    private readonly RtcpHeader _header;
    private readonly ulong _ntpTimestamp;
    private readonly uint _rtpTimestamp;
    private readonly uint _ssrc;
    private readonly uint _packetCount;
    private readonly uint _octetCount;
    private readonly List<ReceptionReportSample> _receptionReports;

    /// <summary>
    /// Create a new RTCP Sender Report from a serialised byte array.
    /// </summary>
    /// <param name="packet">The byte array holding the serialised sender report.</param>
    public RtcpSenderReport(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < MIN_PACKET_SIZE)
        {
            throw new ApplicationException("The packet did not contain the minimum number of bytes for an RtcpSenderReport packet.");
        }

        _header = new RtcpHeader(packet);
        _receptionReports = new List<ReceptionReportSample>();

        _ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet[4..]);
        _ntpTimestamp = BinaryPrimitives.ReadUInt64BigEndian(packet[8..]);
        _rtpTimestamp = BinaryPrimitives.ReadUInt32BigEndian(packet[16..]);
        _packetCount = BinaryPrimitives.ReadUInt32BigEndian(packet[20..]);
        _octetCount = BinaryPrimitives.ReadUInt32BigEndian(packet[24..]);

        var rrIndex = 28;
        for (var i = 0; i < _header.ReceptionReportCount; i++)
        {
            var rr = new ReceptionReportSample(packet[(rrIndex + i * ReceptionReportSample.PAYLOAD_SIZE)..]);
            _receptionReports.Add(rr);
        }
    }

    public byte[] GetBytes()
    {
        var rrCount = _receptionReports != null ? _receptionReports.Count : 0;
        var buffer = new byte[RtcpHeader.HEADER_BYTES_LENGTH + 4 + SENDER_PAYLOAD_SIZE + rrCount * ReceptionReportSample.PAYLOAD_SIZE];
        _header.SetLength((ushort)(buffer.Length / 4 - 1));

        Buffer.BlockCopy(_header.GetBytes(), 0, buffer, 0, RtcpHeader.HEADER_BYTES_LENGTH);
        var payloadIndex = RtcpHeader.HEADER_BYTES_LENGTH;

        var payloadSpan = buffer.AsSpan(payloadIndex);
        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(0, 4), _ssrc);
        BinaryPrimitives.WriteUInt64BigEndian(payloadSpan.Slice(4, 8), _ntpTimestamp);
        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(12, 4), _rtpTimestamp);
        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(16, 4), _packetCount);
        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(20, 4), _octetCount);

        var bufferIndex = payloadIndex + 24;
        for (var i = 0; i < rrCount; i++)
        {
            var receptionReportBytes = _receptionReports[i].GetBytes();
            Buffer.BlockCopy(receptionReportBytes, 0, buffer, bufferIndex, ReceptionReportSample.PAYLOAD_SIZE);
            bufferIndex += ReceptionReportSample.PAYLOAD_SIZE;
        }

        return buffer;
    }
}