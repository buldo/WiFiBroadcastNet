//-----------------------------------------------------------------------------
// Filename: RtcpReceiverReport.cs
//
// Description:
//
//        RTCP Receiver Report Packet
//  0                   1                   2                   3
//         0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
// header |V=2|P|    RC   |   PT=RR=201   |             length            |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                     SSRC of packet sender                     |
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
// report |                 SSRC_2(SSRC of second source)                 |
// block  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//  2     :                               ...                             :
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//        |                  profile-specific extensions                  |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//
//  An empty RR packet (RC = 0) MUST be put at the head of a compound
//  RTCP packet when there is no data transmission or reception to
//  report.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 28 Dec 2019  Aaron Clauson   Created, Dublin, Ireland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Buffers.Binary;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

internal class RtcpReceiverReport
{
    private const int MIN_PACKET_SIZE = RtcpHeader.HEADER_BYTES_LENGTH + 4;

    private readonly uint _ssrc;
    private readonly RtcpHeader _header;
    private readonly List<ReceptionReportSample> _receptionReports;

    /// <summary>
    /// Create a new RTCP Receiver Report from a serialised byte array.
    /// </summary>
    /// <param name="packet">The byte array holding the serialised receiver report.</param>
    public RtcpReceiverReport(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < MIN_PACKET_SIZE)
        {
            throw new ApplicationException("The packet did not contain the minimum number of bytes for an RtcpReceiverReport packet.");
        }

        _header = new RtcpHeader(packet);
        _receptionReports = new List<ReceptionReportSample>();

        _ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(4));

        var rrIndex = 8;
        for (var i = 0; i < _header.ReceptionReportCount; i++)
        {
            var rr = new ReceptionReportSample(packet.Slice(rrIndex + i * ReceptionReportSample.PAYLOAD_SIZE));
            _receptionReports.Add(rr);
        }
    }

    /// <summary>
    /// Gets the serialised bytes for this Receiver Report.
    /// </summary>
    /// <returns>A byte array.</returns>
    public byte[] GetBytes()
    {
        var rrCount = _receptionReports != null ? _receptionReports.Count : 0;
        var buffer = new byte[RtcpHeader.HEADER_BYTES_LENGTH + 4 + rrCount * ReceptionReportSample.PAYLOAD_SIZE];
        _header.SetLength((ushort)(buffer.Length / 4 - 1));

        Buffer.BlockCopy(_header.GetBytes(), 0, buffer, 0, RtcpHeader.HEADER_BYTES_LENGTH);
        var payloadIndex = RtcpHeader.HEADER_BYTES_LENGTH;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(payloadIndex, 4), _ssrc);

        var bufferIndex = payloadIndex + 4;
        for (var i = 0; i < rrCount; i++)
        {
            var receptionReportBytes = _receptionReports[i].GetBytes();
            Buffer.BlockCopy(receptionReportBytes, 0, buffer, bufferIndex, ReceptionReportSample.PAYLOAD_SIZE);
            bufferIndex += ReceptionReportSample.PAYLOAD_SIZE;
        }

        return buffer;
    }
}