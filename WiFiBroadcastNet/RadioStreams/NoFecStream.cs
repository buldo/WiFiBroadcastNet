using System.Buffers.Binary;

namespace WiFiBroadcastNet.RadioStreams;

public class NoFecStream : IRadioStream
{
    private readonly IStreamAccessor _streamAccessor;
    private const int FEC_DISABLED_MAX_SIZE_OF_MAP = 100;
    private readonly HashSet<UInt64> m_known_sequence_numbers = new();
    private bool first_ever_packet = true;

    public NoFecStream(int id, IStreamAccessor streamAccessor)
    {
        _streamAccessor = streamAccessor;
        Id = id;
    }

    public int Id { get; }

    public void ProcessFrame(ReadOnlyMemory<byte> decryptedPayload)
    {
        if (decryptedPayload.Length < 8 + 1)
        {
            // not a valid packet
            return;
        }

        var sequence_number = BinaryPrimitives.ReadUInt64LittleEndian(decryptedPayload.Span.Slice(0, 8));
        process_packet_seq_nr_and_payload(sequence_number, decryptedPayload.Slice(8));
    }

    //No duplicates, but packets out of order are possible
    //counting lost packets doesn't work in this mode. It should be done by the upper level
    //saves the last FEC_DISABLED_MAX_SIZE_OF_MAP sequence numbers. If the sequence number of a new packet is already inside the map, it is discarded (duplicate)
    private void process_packet_seq_nr_and_payload(UInt64 packetSeq, ReadOnlyMemory<byte> payload)
    {
        if (first_ever_packet)
        {
            // first ever packet. Map should be empty
            m_known_sequence_numbers.Clear();
            _streamAccessor.ProcessIncomingFrame(payload);
            m_known_sequence_numbers.Add(packetSeq);
            first_ever_packet = false;
        }

        // check if packet is already known (inside the map)
        if (!m_known_sequence_numbers.Contains(packetSeq))
        {
            // if packet is not in the map it was not yet received(unless it is older than MAX_SIZE_OF_MAP, but that is basically impossible)
            _streamAccessor.ProcessIncomingFrame(payload);
            m_known_sequence_numbers.Add(packetSeq);
        }// else this is a duplicate

        // house keeping, do not increase size to infinity
        if (m_known_sequence_numbers.Count >= FEC_DISABLED_MAX_SIZE_OF_MAP - 1)
        {
            // remove oldest element
            m_known_sequence_numbers.Clear();
        }
    }
}