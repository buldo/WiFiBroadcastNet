using System;
using Microsoft.Extensions.Logging;
using Nito.Collections;
using WiFiBroadcastNet.RadioStreams;

namespace WiFiBroadcastNet.Fec;

// Takes a continuous stream of packets (data and fec correction packets) and
// processes them such that the output is exactly (or as close as possible) to the
// Input stream fed to FECEncoder.
// Most importantly, it also handles re-ordering of packets and packet duplicates due to multiple rx cards
internal class FECDecoder
{
    private readonly ILogger _logger;

    // A value too high doesn't really give much benefit and increases memory usage
    private readonly uint RX_QUEUE_MAX_SIZE;
    private readonly int maxNFragmentsPerBlock;
    private readonly bool m_enable_log_debug;

    // since we also need to search this data structure, a std::queue is not enough.
    // since we have an upper limit on the size of this dequeue, it is basically a searchable ring buffer
    private Deque<RxBlock> rx_queue = new();
    private UInt64 last_known_block = UInt64.MaxValue;  //id of last known block

    /// <summary>
    ///
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="rx_queue_max_depth">
    /// max size of rx queue - since in case of openhd, one frame is either one or two FEC blocks
    /// we don't need that big of an rx queue
    /// </param>
    /// <param name="maxNFragmentsPerBlock">
    /// memory per block is pre-allocated, reduce this value if you know the encoder doesn't ever exceed a given
    /// n of fragments per block
    /// </param>
    /// <param name="enable_log_debug"></param>
    public FECDecoder(
        ILogger logger,
        uint rx_queue_max_depth,
        int maxNFragmentsPerBlock = FecConsts.MAX_TOTAL_FRAGMENTS_PER_BLOCK,
        bool enable_log_debug = false)
    {
        _logger = logger;
        RX_QUEUE_MAX_SIZE = rx_queue_max_depth;
        this.maxNFragmentsPerBlock = maxNFragmentsPerBlock;
        m_enable_log_debug = enable_log_debug;
        //assert(rx_queue_max_depth < 20);
        //assert(rx_queue_max_depth >= 1);
    }

    //// WARNING: Don't forget to register this callback !
    public Action<byte[]> mSendDecodedPayloadCallback;

    //AvgCalculator m_fec_decode_time { };

    public static bool validate_packet_size(int data_len)
    {
        //if (data_len < sizeof(FECPayloadHdr))
        if (data_len < 8)
        {
            // packet is too small
            return false;
        }
        if (data_len > FecConsts.MAX_PAYLOAD_BEFORE_FEC)
        {
            // packet is too big
            return false;
        }
        return true;
    }

    // process a valid packet
    public bool process_valid_packet(byte[] data, int data_len)
    {
        //assert(validate_packet_size(data_len));
        // reconstruct the data layout
        FECPayloadHdr header_p = FecPayloadHelper.CreateFromArray(data);
        /* const uint8_t* payload_p=data+sizeof(FECPayloadHdr);
         const int payload_size=data_len-sizeof(FECPayloadHdr);*/
        if (header_p.fragment_idx >= maxNFragmentsPerBlock)
        {
            _logger.LogWarning($"invalid fragment_idx: {header_p.fragment_idx}");
            return false;
        }
        process_with_rx_queue(header_p, data, data_len);
        return true;
    }

    /**
     * For this Block,
     * starting at the primary fragment we stopped on last time,
     * forward as many primary fragments as they are available until there is a gap
     * @param discardMissingPackets : if true, gaps are ignored and fragments are forwarded even though this means the missing ones are irreversible lost
     * Be carefully with this param, use it only before you need to get rid of a block
     */
    void forwardMissingPrimaryFragmentsIfAvailable(RxBlock block, bool discardMissingPackets = false)
    {
        //assert(mSendDecodedPayloadCallback);
        // TODO remove me
        if (discardMissingPackets)
        {
            if (m_enable_log_debug)
            {
                //wifibroadcast::log::get_default()->warn("Forwarding block that is not yet fully finished: {} total: {} available: {} missing: {}",
                //    block.getBlockIdx(), block.get_n_primary_fragments(), block.getNAvailableFragments(), block.get_missing_primary_packets_readable());
            }
        }
        var indices = block.pullAvailablePrimaryFragments(discardMissingPackets);

        foreach (var primaryFragmentIndex in indices)
        {
            var data = block.get_primary_fragment_data_p(primaryFragmentIndex);
            int data_size = block.get_primary_fragment_data_size(primaryFragmentIndex);
            if (data_size > FecConsts.FEC_PACKET_MAX_PAYLOAD_SIZE || data_size <= 0)
            {
                _logger.LogWarning(
                    "corrupted packet on FECDecoder out ({BlockIdx}:{PrimaryFragmentIndex}) : {DataSize}B",
                    block.getBlockIdx(), primaryFragmentIndex, data_size);
            }
            else
            {
                mSendDecodedPayloadCallback(data.Slice(0, data_size).ToArray());
                //stats.count_bytes_forwarded += data_size;
            }
        }
    }

    // also increase lost block count if block is not fully recovered
    void rxQueuePopFront()
    {
        var front = rx_queue.RemoveFromFront();
        //assert(rx_queue.front() != nullptr);
        if (!front.allPrimaryFragmentsHaveBeenForwarded())
        {
            //stats.count_blocks_lost++;
            if (m_enable_log_debug)
            {
                //auto & block = *rx_queue.front();
                _logger.LogDebug("Removing block {BlockIdx} {MissingPrimaryPackets}", front.getBlockIdx(), front.get_missing_primary_packets_readable());
            }
        }
    }

    // create a new RxBlock for the specified block_idx and push it into the queue
    // NOTE: Checks first if this operation would increase the size of the queue over its max capacity
    // In this case, the only solution is to remove the oldest block before adding the new one
    void rxRingCreateNewSafe(UInt64 blockIdx)
    {
        // check: make sure to always put blocks into the queue in order !
        if (rx_queue.Count != 0)
        {
            // the newest block in the queue should be equal to block_idx -1
            // but it must not ?!
            if (rx_queue.Last().getBlockIdx() != (blockIdx - 1))
            {
                // If we land here, one or more full blocks are missing, which can happen on bad rx links
                //wifibroadcast::log::get_default()->debug("In queue: {} But new: {}",rx_queue.back()->getBlockIdx(),blockIdx);
            }
            //assert(rx_queue.back()->getBlockIdx() == (blockIdx - 1));
        }
        // we can return early if this operation doesn't exceed the size limit
        if (rx_queue.Count < RX_QUEUE_MAX_SIZE)
        {
            rx_queue.AddToBack(new RxBlock(maxNFragmentsPerBlock, blockIdx));
            //stats.count_blocks_total++;
            return;
        }
        //Ring overflow. This means that there are more unfinished blocks than ring size
        //Possible solutions:
        //1. Increase ring size. Do this if you have large variance of packet travel time throught WiFi card or network stack.
        //   Some cards can do this due to packet reordering inside, diffent chipset and/or firmware or your RX hosts have different CPU power.
        //2. Reduce packet injection speed or try to unify RX hardware.

        // forward remaining data for the (oldest) block, since we need to get rid of it
        var oldestBlock = rx_queue.First();
        forwardMissingPrimaryFragmentsIfAvailable(oldestBlock, true);
        // and remove the block once done with it
        rxQueuePopFront();

        // now we are guaranteed to have space for one new block
        rx_queue.AddToBack(new RxBlock(maxNFragmentsPerBlock, blockIdx));
        //stats.count_blocks_total++;
    }

    // If block is already known and not in the queue anymore return nullptr
    // else if block is inside the ring return pointer to it
    // and if it is not inside the ring add as many blocks as needed, then return pointer to it
    RxBlock? rxRingFindCreateBlockByIdx(UInt64 blockIdx)
    {
        var found = rx_queue.FirstOrDefault(b => b.getBlockIdx() == blockIdx);
        if (found != null)
        {
            return found;
        }
        // check if block is already known and not in the ring then it is already processed
        if (last_known_block != UInt64.MaxValue  && blockIdx <= last_known_block)
        {
            return null;
        }

        // don't forget to increase the lost blocks counter if we do not add blocks here due to no space in the rx queue
        // (can happen easily if the rx queue has a size of 1)
        var n_needed_new_blocks = last_known_block != UInt64.MaxValue ? blockIdx - last_known_block : 1;
        if (n_needed_new_blocks > RX_QUEUE_MAX_SIZE)
        {
            if (m_enable_log_debug)
            {
                _logger.LogDebug("Need {CntNeededNewBlocks} blocks, exceeds {RX_QUEUE_MAX_SIZE}", n_needed_new_blocks, RX_QUEUE_MAX_SIZE);
            }
            //stats.count_blocks_lost += n_needed_new_blocks - RX_QUEUE_MAX_SIZE;
        }
        // add as many blocks as we need ( the rx ring mustn't have any gaps between the block indices).
        // but there is no point in adding more blocks than RX_RING_SIZE
        UInt64 new_blocks = UInt64.Min(n_needed_new_blocks, RX_QUEUE_MAX_SIZE);
        last_known_block = blockIdx;

        for (UInt64 i = 0; i < new_blocks; i++)
        {
            rxRingCreateNewSafe(blockIdx + i + 1 - new_blocks);
        }
        // the new block we've added is now the most recently added element (and since we always push to the back, the "back()" element)
        //assert(rx_queue.back()->getBlockIdx() == blockIdx);
        return rx_queue.Last();
    }

    void process_with_rx_queue(FECPayloadHdr header, byte[] data, int data_size)
    {
        var blockP = rxRingFindCreateBlockByIdx(header.block_idx);
        //ignore already processed blocks
        if (blockP == null)
        {
            return;
        }
        // cannot be nullptr
        RxBlock block = blockP;
        // ignore already processed fragments
        if (block.hasFragment(header.fragment_idx))
        {
            return;
        }
        block.addFragment(data, data_size);
        if (block == rx_queue.First())
        {
            //wifibroadcast::log::get_default()->debug("In front\n";
            // we are in the front of the queue (e.g. at the oldest block)
            // forward packets until the first gap
            forwardMissingPrimaryFragmentsIfAvailable(block);
            // We are done with this block if either all fragments have been forwarded or it can be recovered
            if (block.allPrimaryFragmentsHaveBeenForwarded())
            {
                // remove block when done with it
                rxQueuePopFront();
                return;
            }
            if (block.allPrimaryFragmentsCanBeRecovered())
            {
                // apply fec for this block
                //const auto before_encode = std::chrono::steady_clock::now();
                block.reconstructAllMissingData();
                //stats.count_fragments_recovered += block.reconstructAllMissingData();
                //stats.count_blocks_recovered++;
                //m_fec_decode_time.add(std::chrono::steady_clock::now() - before_encode);
                //if (m_fec_decode_time.get_delta_since_last_reset() > std::chrono::seconds(1))
                //{
                //    //wifibroadcast::log::get_default()->debug("FEC decode took {}",m_fec_decode_time.getAvgReadable());
                //    stats.curr_fec_decode_time = m_fec_decode_time.getMinMaxAvg();
                //    m_fec_decode_time.reset();
                //}
                forwardMissingPrimaryFragmentsIfAvailable(block);
                //assert(block.allPrimaryFragmentsHaveBeenForwarded());
                // remove block when done with it
                rxQueuePopFront();
                return;
            }
            return;
        }
        else
        {
            //wifibroadcast::log::get_default()->debug("Not in front\n";
            // we are not in the front of the queue but somewhere else
            // If this block can be fully recovered or all primary fragments are available this triggers a flush
            if (block.allPrimaryFragmentsAreAvailable() || block.allPrimaryFragmentsCanBeRecovered())
            {
                // send all queued packets in all unfinished blocks before and remove them
                if (m_enable_log_debug)
                {
                    _logger.LogDebug("Block {BlockIdx} triggered a flush", block.getBlockIdx());
                }
                while (block != rx_queue.First())
                {
                    forwardMissingPrimaryFragmentsIfAvailable(rx_queue.First(), true);
                    rxQueuePopFront();
                }
                // then process the block who is fully recoverable or has no gaps in the primary fragments
                if (block.allPrimaryFragmentsAreAvailable())
                {
                    forwardMissingPrimaryFragmentsIfAvailable(block);
                    //assert(block.allPrimaryFragmentsHaveBeenForwarded());
                }
                else
                {
                    // apply fec for this block
                    block.reconstructAllMissingData();
                    //stats.count_fragments_recovered += block.reconstructAllMissingData();
                    //stats.count_blocks_recovered++;
                    forwardMissingPrimaryFragmentsIfAvailable(block);
                    //assert(block.allPrimaryFragmentsHaveBeenForwarded());
                }
                // remove block
                rxQueuePopFront();
            }
        }
    }

    //  public:
    //// matches FECDecoder
    //struct FECRxStats
    //  {
    //      // total block count
    //      uint64_t count_blocks_total = 0;
    //      // a block counts as "lost" if it was removed before being fully received or recovered
    //      uint64_t count_blocks_lost = 0;
    //      // a block counts as "recovered" if it was recovered using FEC packets
    //      uint64_t count_blocks_recovered = 0;
    //      // n of primary fragments that were reconstructed during the recovery process of a block
    //      uint64_t count_fragments_recovered = 0;
    //      // n of forwarded bytes
    //      uint64_t count_bytes_forwarded = 0;
    //      MinMaxAvg<std::chrono::nanoseconds> curr_fec_decode_time { };
    //  };
    //  FECRxStats stats { };
    void reset_rx_queue()
    {
        rx_queue.Clear();
        last_known_block = UInt64.MaxValue;
    }
}
