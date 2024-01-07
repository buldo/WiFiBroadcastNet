using System;
using Microsoft.Extensions.Logging;
using Nito.Collections;
using WiFiBroadcastNet.RadioStreams;

namespace WiFiBroadcastNet.Fec;

// Takes a continuous stream of packets (data and fec correction packets) and
// processes them such that the output is exactly (or as close as possible) to the
// Input stream fed to FECEncoder.
// Most importantly, it also handles re-ordering of packets and packet duplicates due to multiple rx cards
internal class FecDecoder
{
    private readonly ILogger _logger;

    // A value too high doesn't really give much benefit and increases memory usage
    private readonly uint _rxQueueMaxSize;
    private readonly int _maxFragmentsPerBlock;
    private readonly bool _mEnableLogDebug;

    // since we also need to search this data structure, a std::queue is not enough.
    // since we have an upper limit on the size of this dequeue, it is basically a searchable ring buffer
    private readonly Deque<RxBlock> _rxQueue = new();
    private readonly Queue<RxBlock> _freeBlocks = new();
    private UInt64 _lastKnownBlock = UInt64.MaxValue;  //id of last known block

    /// <summary>
    ///
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="rxQueueMaxDepth">
    /// max size of rx queue - since in case of openhd, one frame is either one or two FEC blocks
    /// we don't need that big of an rx queue
    /// </param>
    /// <param name="maxFragmentsPerBlock">
    /// memory per block is pre-allocated, reduce this value if you know the encoder doesn't ever exceed a given
    /// n of fragments per block
    /// </param>
    /// <param name="enableLogDebug"></param>
    public FecDecoder(
        ILogger logger,
        uint rxQueueMaxDepth,
        int maxFragmentsPerBlock = FecConsts.MAX_TOTAL_FRAGMENTS_PER_BLOCK,
        bool enableLogDebug = false)
    {
        _logger = logger;
        _rxQueueMaxSize = rxQueueMaxDepth;
        _maxFragmentsPerBlock = maxFragmentsPerBlock;
        _mEnableLogDebug = enableLogDebug;
        //assert(rx_queue_max_depth < 20);
        //assert(rx_queue_max_depth >= 1);
    }

    //// WARNING: Don't forget to register this callback !
    public Action<byte[]> _sendDecodedPayloadCallback;

    //AvgCalculator m_fec_decode_time { };

    public static bool ValidatePacketSize(int dataLen)
    {
        //if (data_len < sizeof(FECPayloadHdr))
        if (dataLen < 8)
        {
            // packet is too small
            return false;
        }
        if (dataLen > FecConsts.MAX_PAYLOAD_BEFORE_FEC)
        {
            // packet is too big
            return false;
        }
        return true;
    }

    // process a valid packet
    public bool ProcessValidPacket(ReadOnlySpan<byte> data)
    {
        //assert(ValidatePacketSize(data_len));
        // reconstruct the data layout
        FECPayloadHdr headerP = FecPayloadHelper.CreateFromArray(data);
        /* const uint8_t* payload_p=data+sizeof(FECPayloadHdr);
         const int payload_size=data_len-sizeof(FECPayloadHdr);*/
        if (headerP.fragment_idx >= _maxFragmentsPerBlock)
        {
            _logger.LogWarning($"invalid fragment_idx: {headerP.fragment_idx}");
            return false;
        }
        ProcessWithRxQueue(headerP, data);
        return true;
    }

    /**
     * For this Block,
     * starting at the primary fragment we stopped on last time,
     * forward as many primary fragments as they are available until there is a gap
     * @param discardMissingPackets : if true, gaps are ignored and fragments are forwarded even though this means the missing ones are irreversible lost
     * Be carefully with this param, use it only before you need to get rid of a block
     */
    private void ForwardMissingPrimaryFragmentsIfAvailable(RxBlock block, bool discardMissingPackets = false)
    {
        //assert(mSendDecodedPayloadCallback);
        // TODO remove me
        if (discardMissingPackets)
        {
            if (_mEnableLogDebug)
            {
                //wifibroadcast::log::get_default()->warn("Forwarding block that is not yet fully finished: {} total: {} available: {} missing: {}",
                //    block.getBlockIdx(), block.get_n_primary_fragments(), block.getNAvailableFragments(), block.get_missing_primary_packets_readable());
            }
        }
        var indices = block.PullAvailablePrimaryFragments(discardMissingPackets);

        foreach (var primaryFragmentIndex in indices)
        {
            var data = block.get_primary_fragment_data_p(primaryFragmentIndex);
            int dataSize = block.get_primary_fragment_data_size(primaryFragmentIndex);
            if (dataSize > FecConsts.FEC_PACKET_MAX_PAYLOAD_SIZE || dataSize <= 0)
            {
                _logger.LogWarning(
                    "corrupted packet on FECDecoder out ({BlockIdx}:{PrimaryFragmentIndex}) : {DataSize}B",
                    block.GetBlockIdx(), primaryFragmentIndex, dataSize);
            }
            else
            {
                _sendDecodedPayloadCallback(data.Slice(0, dataSize).ToArray());
                //stats.count_bytes_forwarded += data_size;
            }
        }
    }

    // also increase lost block count if block is not fully recovered
    private void RxQueuePopFront()
    {
        var front = _rxQueue.RemoveFromFront();
        //assert(rx_queue.front() != nullptr);
        if (!front.AllPrimaryFragmentsHaveBeenForwarded())
        {
            //stats.count_blocks_lost++;
            if (_mEnableLogDebug)
            {
                //auto & block = *rx_queue.front();
                _logger.LogDebug("Removing block {BlockIdx} {MissingPrimaryPackets}", front.GetBlockIdx(), front.get_missing_primary_packets_readable());
            }
        }

        _freeBlocks.Enqueue(front);
    }

    // create a new RxBlock for the specified block_idx and push it into the queue
    // NOTE: Checks first if this operation would increase the size of the queue over its max capacity
    // In this case, the only solution is to remove the oldest block before adding the new one
    private void RxRingCreateNewSafe(UInt64 blockIdx)
    {
        // check: make sure to always put blocks into the queue in order !
        if (_rxQueue.Count != 0)
        {
            // the newest block in the queue should be equal to block_idx -1
            // but it must not ?!
            if (_rxQueue.Last().GetBlockIdx() != (blockIdx - 1))
            {
                // If we land here, one or more full blocks are missing, which can happen on bad rx links
                //wifibroadcast::log::get_default()->debug("In queue: {} But new: {}",rx_queue.back()->getBlockIdx(),blockIdx);
            }
            //assert(rx_queue.back()->getBlockIdx() == (blockIdx - 1));
        }
        // we can return early if this operation doesn't exceed the size limit
        if (_rxQueue.Count < _rxQueueMaxSize)
        {
            _rxQueue.AddToBack(GetReadyToWorkBlock(blockIdx));
            //stats.count_blocks_total++;
            return;
        }
        //Ring overflow. This means that there are more unfinished blocks than ring size
        //Possible solutions:
        //1. Increase ring size. Do this if you have large variance of packet travel time throught WiFi card or network stack.
        //   Some cards can do this due to packet reordering inside, diffent chipset and/or firmware or your RX hosts have different CPU power.
        //2. Reduce packet injection speed or try to unify RX hardware.

        // forward remaining data for the (oldest) block, since we need to get rid of it
        var oldestBlock = _rxQueue.First();
        ForwardMissingPrimaryFragmentsIfAvailable(oldestBlock, true);
        // and remove the block once done with it
        RxQueuePopFront();

        // now we are guaranteed to have space for one new block
        _rxQueue.AddToBack(GetReadyToWorkBlock(blockIdx));
        //stats.count_blocks_total++;
    }

    // If block is already known and not in the queue anymore return nullptr
    // else if block is inside the ring return pointer to it
    // and if it is not inside the ring add as many blocks as needed, then return pointer to it
    private RxBlock? RxRingFindCreateBlockByIdx(UInt64 blockIdx)
    {
        var found = _rxQueue.FirstOrDefault(b => b.GetBlockIdx() == blockIdx);
        if (found != null)
        {
            return found;
        }
        // check if block is already known and not in the ring then it is already processed
        if (_lastKnownBlock != UInt64.MaxValue  && blockIdx <= _lastKnownBlock)
        {
            return null;
        }

        // don't forget to increase the lost blocks counter if we do not add blocks here due to no space in the rx queue
        // (can happen easily if the rx queue has a size of 1)
        var nNeededNewBlocks = _lastKnownBlock != UInt64.MaxValue ? blockIdx - _lastKnownBlock : 1;
        if (nNeededNewBlocks > _rxQueueMaxSize)
        {
            if (_mEnableLogDebug)
            {
                _logger.LogDebug("Need {CntNeededNewBlocks} blocks, exceeds {RX_QUEUE_MAX_SIZE}", nNeededNewBlocks, _rxQueueMaxSize);
            }
            //stats.count_blocks_lost += n_needed_new_blocks - RX_QUEUE_MAX_SIZE;
        }
        // add as many blocks as we need ( the rx ring mustn't have any gaps between the block indices).
        // but there is no point in adding more blocks than RX_RING_SIZE
        UInt64 newBlocks = UInt64.Min(nNeededNewBlocks, _rxQueueMaxSize);
        _lastKnownBlock = blockIdx;

        for (UInt64 i = 0; i < newBlocks; i++)
        {
            RxRingCreateNewSafe(blockIdx + i + 1 - newBlocks);
        }
        // the new block we've added is now the most recently added element (and since we always push to the back, the "back()" element)
        //assert(rx_queue.back()->getBlockIdx() == blockIdx);
        return _rxQueue.Last();
    }

    private void ProcessWithRxQueue(FECPayloadHdr header, ReadOnlySpan<byte> data)
    {
        var blockP = RxRingFindCreateBlockByIdx(header.block_idx);
        //ignore already processed blocks
        if (blockP == null)
        {
            return;
        }
        // cannot be nullptr
        RxBlock block = blockP;
        // ignore already processed fragments
        if (block.HasFragment(header.fragment_idx))
        {
            return;
        }
        block.AddFragment(data);
        if (block == _rxQueue.First())
        {
            //wifibroadcast::log::get_default()->debug("In front\n";
            // we are in the front of the queue (e.g. at the oldest block)
            // forward packets until the first gap
            ForwardMissingPrimaryFragmentsIfAvailable(block);
            // We are done with this block if either all fragments have been forwarded or it can be recovered
            if (block.AllPrimaryFragmentsHaveBeenForwarded())
            {
                // remove block when done with it
                RxQueuePopFront();
                return;
            }
            if (block.AllPrimaryFragmentsCanBeRecovered())
            {
                // apply fec for this block
                //const auto before_encode = std::chrono::steady_clock::now();
                block.ReconstructAllMissingData();
                //stats.count_fragments_recovered += block.reconstructAllMissingData();
                //stats.count_blocks_recovered++;
                //m_fec_decode_time.add(std::chrono::steady_clock::now() - before_encode);
                //if (m_fec_decode_time.get_delta_since_last_reset() > std::chrono::seconds(1))
                //{
                //    //wifibroadcast::log::get_default()->debug("FEC decode took {}",m_fec_decode_time.getAvgReadable());
                //    stats.curr_fec_decode_time = m_fec_decode_time.getMinMaxAvg();
                //    m_fec_decode_time.reset();
                //}
                ForwardMissingPrimaryFragmentsIfAvailable(block);
                //assert(block.allPrimaryFragmentsHaveBeenForwarded());
                // remove block when done with it
                RxQueuePopFront();
                return;
            }
            return;
        }
        else
        {
            //wifibroadcast::log::get_default()->debug("Not in front\n";
            // we are not in the front of the queue but somewhere else
            // If this block can be fully recovered or all primary fragments are available this triggers a flush
            if (block.AllPrimaryFragmentsAreAvailable() || block.AllPrimaryFragmentsCanBeRecovered())
            {
                // send all queued packets in all unfinished blocks before and remove them
                if (_mEnableLogDebug)
                {
                    _logger.LogDebug("Block {BlockIdx} triggered a flush", block.GetBlockIdx());
                }
                while (block != _rxQueue.First())
                {
                    ForwardMissingPrimaryFragmentsIfAvailable(_rxQueue.First(), true);
                    RxQueuePopFront();
                }
                // then process the block who is fully recoverable or has no gaps in the primary fragments
                if (block.AllPrimaryFragmentsAreAvailable())
                {
                    ForwardMissingPrimaryFragmentsIfAvailable(block);
                    //assert(block.allPrimaryFragmentsHaveBeenForwarded());
                }
                else
                {
                    // apply fec for this block
                    block.ReconstructAllMissingData();
                    //stats.count_fragments_recovered += block.reconstructAllMissingData();
                    //stats.count_blocks_recovered++;
                    ForwardMissingPrimaryFragmentsIfAvailable(block);
                    //assert(block.allPrimaryFragmentsHaveBeenForwarded());
                }
                // remove block
                RxQueuePopFront();
            }
        }
    }

    void reset_rx_queue()
    {
        _rxQueue.Clear();
        _lastKnownBlock = UInt64.MaxValue;
    }

    private RxBlock GetReadyToWorkBlock(UInt64 blockIdx)
    {
        if (!_freeBlocks.TryDequeue(out var block))
        {
            block = new RxBlock(_maxFragmentsPerBlock);
        }

        block.ReInit(blockIdx);

        return block;
    }
}
