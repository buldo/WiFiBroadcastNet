using System.Buffers.Binary;
using System.Text;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// RTCP Goodbye packet as defined in RFC3550. The BYE packet indicates
/// that one or more sources are no longer active.
/// </summary>
internal class RtcpBye
{
    private const int SSRC_SIZE = 4;       // 4 bytes for the SSRC.
    private const int MIN_PACKET_SIZE = RtcpHeader.HEADER_BYTES_LENGTH + SSRC_SIZE;

    private readonly RtcpHeader _header;
    public uint Ssrc { get; }
    public string Reason { get; }

    /// <summary>
    /// Create a new RTCP Goodbye packet from a serialised byte array.
    /// </summary>
    /// <param name="packet">The byte array holding the Goodbye packet.</param>
    public RtcpBye(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < MIN_PACKET_SIZE)
        {
            throw new ApplicationException("The packet did not contain the minimum number of bytes for an RTCP Goodbye packet.");
        }

        _header = new RtcpHeader(packet);

        Ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(4));

        if (packet.Length > MIN_PACKET_SIZE)
        {
            int reasonLength = packet[8];

            if (packet.Length - MIN_PACKET_SIZE - 1 >= reasonLength)
            {
                Reason = Encoding.UTF8.GetString(packet.Slice(9,reasonLength));
            }
        }
    }

    /// <summary>
    /// Gets the raw bytes for the Goodbye packet.
    /// </summary>
    /// <returns>A byte array.</returns>
    public byte[] GetBytes()
    {
        var reasonBytes = Reason != null ? Encoding.UTF8.GetBytes(Reason) : null;
        var reasonLength = reasonBytes != null ? reasonBytes.Length : 0;
        var buffer = new byte[RtcpHeader.HEADER_BYTES_LENGTH + GetPaddedLength(reasonLength)];
        _header.SetLength((ushort)(buffer.Length / 4 - 1));

        Buffer.BlockCopy(_header.GetBytes(), 0, buffer, 0, RtcpHeader.HEADER_BYTES_LENGTH);
        var payloadIndex = RtcpHeader.HEADER_BYTES_LENGTH;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(payloadIndex, 4), Ssrc);

        if (reasonLength > 0)
        {
            buffer[payloadIndex + 4] = (byte)reasonLength;
            Buffer.BlockCopy(reasonBytes, 0, buffer, payloadIndex + 5, reasonBytes.Length);
        }

        return buffer;
    }

    /// <summary>
    /// The packet has to finish on a 4 byte boundary. This method calculates the minimum
    /// packet length for the Goodbye fields to fit within a 4 byte boundary.
    /// </summary>
    /// <param name="reasonLength">The length of the optional reason string, can be 0.</param>
    /// <returns>The minimum length for the full packet to be able to fit within a 4 byte
    /// boundary.</returns>
    private int GetPaddedLength(int reasonLength)
    {
        // Plus one is for the reason length field.
        if (reasonLength > 0)
        {
            reasonLength += 1;
        }

        var nonPaddedSize = reasonLength + SSRC_SIZE;

        if (nonPaddedSize % 4 == 0)
        {
            return nonPaddedSize;
        }

        return nonPaddedSize + 4 - nonPaddedSize % 4;
    }
}