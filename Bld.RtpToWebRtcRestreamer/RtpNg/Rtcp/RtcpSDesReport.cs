//-----------------------------------------------------------------------------
// Filename: RtcpSDesReport.cs
//
// Description: RTCP Source Description (SDES) report as defined in RFC3550.
// Only the mandatory CNAME item is supported.
//
//         RTCP SDES Payload
//         0                   1                   2                   3
//         0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
// header |V=2|P|    SC   |  PT=SDES=202  |             length            |
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
// chunk  |                          SSRC/CSRC_1                          |
//  1     +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                           SDES items                          |
//        |                              ...                              |
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
// chunk  |                          SSRC/CSRC_2                          |
//  2     +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                           SDES items                          |
//        |                              ...                              |
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
//    0                   1                   2                   3
//    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//   |    CNAME=1    |     length    | user and domain name        ...
//   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//
//   the CNAME item SHOULD have the format
//   "user@host", or "host" if a user name is not available as on single-
//   user systems.For both formats, "host" is either the fully qualified
//   domain name of the host from which the real-time data originates,
//   formatted according to the rules specified in RFC 1034 [6], RFC 1035
//   [7] and Section 2.1 of RFC 1123 [8]; or the standard ASCII
//   representation of the host's numeric address on the interface used
//   for the RTP communication.
//
//  The list of items in each chunk
//  MUST be terminated by one or more null octets, the first of which is
//  interpreted as an item type of zero to denote the end of the list.
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
using System.Text;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// RTCP Source Description (SDES) report as defined in RFC3550.
/// Only the mandatory CNAME item is supported.
/// </summary>
internal class RtcpSDesReport
{
    private const int PACKET_SIZE_WITHOUT_CNAME = 6; // 4 byte SSRC, 1 byte CNAME ID, 1 byte CNAME length.
    private const byte CNAME_ID = 0x01;
    private const int MIN_PACKET_SIZE = RtcpHeader.HEADER_BYTES_LENGTH + PACKET_SIZE_WITHOUT_CNAME;

    private readonly RtcpHeader _header;
    private readonly uint _ssrc;
    private readonly string _cname;

    /// <summary>
    /// Create a new RTCP SDES item from a serialised byte array.
    /// </summary>
    /// <param name="packet">The byte array holding the SDES report.</param>
    public RtcpSDesReport(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < MIN_PACKET_SIZE)
        {
            throw new ApplicationException("The packet did not contain the minimum number of bytes for an RTCP SDES packet.");
        }

        if (packet[8] != CNAME_ID)
        {
            throw new ApplicationException("The RTCP report packet did not have the required CNAME type field set correctly.");
        }

        _header = new RtcpHeader(packet);

        _ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(4));

        int cnameLength = packet[9];
        _cname = Encoding.UTF8.GetString(packet.Slice(10, cnameLength));
    }

    /// <summary>
    /// Gets the raw bytes for the SDES item. This packet is ready to be included
    /// directly in an RTCP packet.
    /// </summary>
    /// <returns>A byte array containing the serialised SDES item.</returns>
    public byte[] GetBytes()
    {
        var cnameBytes = Encoding.UTF8.GetBytes(_cname);
        var buffer = new byte[RtcpHeader.HEADER_BYTES_LENGTH + GetPaddedLength(cnameBytes.Length)]; // Array automatically initialised with 0x00 values.
        _header.SetLength((ushort)(buffer.Length / 4 - 1));

        Buffer.BlockCopy(_header.GetBytes(), 0, buffer, 0, RtcpHeader.HEADER_BYTES_LENGTH);
        var payloadIndex = RtcpHeader.HEADER_BYTES_LENGTH;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(payloadIndex, 4), _ssrc);

        buffer[payloadIndex + 4] = CNAME_ID;
        buffer[payloadIndex + 5] = (byte)cnameBytes.Length;
        Buffer.BlockCopy(cnameBytes, 0, buffer, payloadIndex + 6, cnameBytes.Length);

        return buffer;
    }

    /// <summary>
    /// The packet has to finish on a 4 byte boundary. This method calculates the minimum
    /// packet length for the SDES fields to fit within a 4 byte boundary.
    /// </summary>
    /// <param name="cnameLength">The length of the cname string.</param>
    /// <returns>The minimum length for the full packet to be able to fit within a 4 byte
    /// boundary.</returns>
    private int GetPaddedLength(int cnameLength)
    {
        // Plus one is for the 0x00 items termination byte.
        var nonPaddedSize = cnameLength + PACKET_SIZE_WITHOUT_CNAME + 1;

        if (nonPaddedSize % 4 == 0)
        {
            return nonPaddedSize;
        }

        return nonPaddedSize + 4 - nonPaddedSize % 4;
    }
}