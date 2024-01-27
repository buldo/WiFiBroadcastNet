using System.Buffers.Binary;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// Represents a point in time sample for a reception report.
/// </summary>
internal class ReceptionReportSample
{
    public const int PAYLOAD_SIZE = 24;

    /// <summary>
    /// Fraction lost since last SR/RR.
    /// </summary>
    private readonly byte _fractionLost;

    /// <summary>
    /// Extended last sequence number received.
    /// </summary>
    private readonly uint _extendedHighestSequenceNumber;

    /// <summary>
    /// Last SR packet from this source.
    /// </summary>
    private readonly uint _lastSenderReportTimestamp;

    /// <summary>
    /// Delay since last SR packet.
    /// </summary>
    private readonly uint _delaySinceLastSenderReport = 0;

    public ReceptionReportSample(ReadOnlySpan<byte> packet)
    {
        {
            Ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet[4..]);
            _fractionLost = packet[4];
            PacketsLost = BinaryPrimitives.ReadInt32BigEndian(new byte[] { 0x00, packet[5], packet[6], packet[7] });
            _extendedHighestSequenceNumber = BinaryPrimitives.ReadUInt32BigEndian(packet[8..]);
            Jitter = BinaryPrimitives.ReadUInt32BigEndian(packet[12..]);
            _lastSenderReportTimestamp = BinaryPrimitives.ReadUInt32BigEndian(packet[16..]);
            _lastSenderReportTimestamp = BinaryPrimitives.ReadUInt32BigEndian(packet[20..]);
        }
    }

    /// <summary>
    /// Data source being reported.
    /// </summary>
    public uint Ssrc { get; }

    /// <summary>
    /// Interarrival jitter.
    /// </summary>
    public uint Jitter { get; }

    /// <summary>
    /// Cumulative number of packets lost (signed!).
    /// </summary>
    public int PacketsLost { get; }

    /// <summary>
    /// Serialises the reception report block to a byte array.
    /// </summary>
    /// <returns>A byte array.</returns>
    public byte[] GetBytes()
    {
        var payload = new byte[24];
        var payloadSpan = payload.AsSpan();

        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(0, 4), Ssrc);
        BinaryPrimitives.WriteInt32BigEndian(payloadSpan.Slice(4, 4), PacketsLost);
        payload[4] = _fractionLost; // It will rewrite first byte of previous line

        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(8, 4), _extendedHighestSequenceNumber);
        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(12, 4), Jitter);
        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(16, 4), _lastSenderReportTimestamp);
        BinaryPrimitives.WriteUInt32BigEndian(payloadSpan.Slice(20, 4), _delaySinceLastSenderReport);

        return payload;
    }
}