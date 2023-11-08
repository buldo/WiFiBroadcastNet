using System;

using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet.RadioStreams;

public class FecStream : IRadioStream
{
    private readonly IStreamAccessor _userStream;
    private readonly FECDecoder _fec = new();

    public FecStream(int id, IStreamAccessor userStream)
    {
        _userStream = userStream;
        Id = id;
    }

    public int Id { get; }

    public void ProcessFrame(Memory<byte> decryptedPayload)
    {
    }
}

// Takes a continuous stream of packets (data and fec correction packets) and
// processes them such that the output is exactly (or as close as possible) to the
// Input stream fed to FECEncoder.
// Most importantly, it also handles re-ordering of packets and packet duplicates due to multiple rx cards
internal class FECDecoder
{
    // max 255 primary and secondary fragments together for now. Theoretically, this implementation has enough bytes in the header for
    // up to 15 bit fragment indices, 2^15=32768
    // Note: currently limited by the fec c implementation
    private const UInt16 MAX_N_P_FRAGMENTS_PER_BLOCK = 128;
    private const UInt16 MAX_N_S_FRAGMENTS_PER_BLOCK = 128;
    private const UInt16 MAX_TOTAL_FRAGMENTS_PER_BLOCK = MAX_N_P_FRAGMENTS_PER_BLOCK + MAX_N_S_FRAGMENTS_PER_BLOCK;

    readonly uint RX_QUEUE_MAX_SIZE;
    readonly uint maxNFragmentsPerBlock;

    UInt64 last_known_block = UInt64.MaxValue;  //id of last known block

    /// <summary>
    ///
    /// </summary>
    /// <param name="rx_queue_max_depth">max size of rx queue - since in case of openhd, one frame is either one or two FEC blocks we don't need that big of an rx queue</param>
    /// <param name="maxNFragmentsPerBlock">memory per block is pre-allocated, reduce this value if you know the encoder doesn't ever exceed a given n of fragments per block</param>
    public FECDecoder(
        UInt32 rx_queue_max_depth,
        UInt32 maxNFragmentsPerBlock = MAX_TOTAL_FRAGMENTS_PER_BLOCK)
    {
        RX_QUEUE_MAX_SIZE = rx_queue_max_depth;
        this.maxNFragmentsPerBlock = maxNFragmentsPerBlock;
        //assert(rx_queue_max_depth < 20);
        //assert(rx_queue_max_depth >= 1);
    }

  // data forwarded on this callback is always in-order but possibly with gaps
  // WARNING: Don't forget to register this callback !

    //SEND_DECODED_PACKET mSendDecodedPayloadCallback;
    // A value too high doesn't really give much benefit and increases memory usage


    //static bool validate_packet_size(int data_len);
    // process a valid packet
    public bool process_valid_packet(Memory<byte> data)
    {

    }

  // since we also need to search this data structure, a std::queue is not enough.
  // since we have an upper limit on the size of this dequeue, it is basically a searchable ring buffer
    private std::deque<std::unique_ptr<RxBlock>> rx_queue;

    /**
     * For this Block,
     * starting at the primary fragment we stopped on last time,
     * forward as many primary fragments as they are available until there is a gap
     * @param discardMissingPackets : if true, gaps are ignored and fragments are forwarded even though this means the missing ones are irreversible lost
     * Be carefully with this param, use it only before you need to get rid of a block
     */
    void forwardMissingPrimaryFragmentsIfAvailable(RxBlock &block, const bool discardMissingPackets = false)
    {

    }
    // also increase lost block count if block is not fully recovered
    void rxQueuePopFront()
    {

    }
    // create a new RxBlock for the specified block_idx and push it into the queue
    // NOTE: Checks first if this operation would increase the size of the queue over its max capacity
    // In this case, the only solution is to remove the oldest block before adding the new one
    void rxRingCreateNewSafe(UInt64 blockIdx)
    {

    }

    // If block is already known and not in the queue anymore return nullptr
    // else if block is inside the ring return pointer to it
    // and if it is not inside the ring add as many blocks as needed, then return pointer to it
    RxBlock* rxRingFindCreateBlockByIdx(const uint64_t blockIdx);
    void process_with_rx_queue(const FECPayloadHdr& header,const uint8_t* data,int data_size);

    void reset_rx_queue();
};