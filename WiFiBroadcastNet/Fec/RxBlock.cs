using System.Buffers.Binary;
using System.Diagnostics;
using OpenHd.Fec;
using WiFiBroadcastNet.RadioStreams;

namespace WiFiBroadcastNet.Fec;

// This encapsulates everything you need when working on a single FEC block on the receiver
// for example, addFragment() or pullAvailablePrimaryFragments()
// it also provides convenient methods to query if the block is fully forwarded
// or if it is ready for the FEC reconstruction step.
class RxBlock
{
    /// <summary>
    /// the block idx marks which block this element refers to
    /// </summary>
    private readonly ulong _blockIdx = 0;

    /// <summary>
    /// for each fragment (via fragment_idx) store if it has been received yet
    /// </summary>
    private readonly bool[] _fragmentMap;

    /// <summary>
    /// holds all the data for all received fragments (if fragment_map says UNAVALIABLE at this position, content is undefined)
    /// </summary>
    private readonly List<byte[]> _blockBuffer;

    /// <summary>
    /// n of primary fragments that are already pulled out
    /// </summary>
    private int _alreadyForwardedPrimaryFragments = 0;

    // time point when the first fragment for this block was received (via addFragment() )
    private DateTime? _firstFragmentTimePoint = null;
    // as soon as we know any of the fragments for this block, we know how many primary fragments this block contains
    // (and therefore, how many primary or secondary fragments we need to fully reconstruct)
    int _primaryFragmentsInBlock = -1;
    // for the fec step, we need the size of the fec secondary fragments, which should be equal for all secondary fragments
    int _sizeOfSecondaryFragments = -1;
    int _availablePrimaryFragments = 0;
    int _availableSecondaryFragments = 0;


    // @param maxNFragmentsPerBlock max number of primary and secondary fragments for this block.
    // you could just use MAX_TOTAL_FRAGMENTS_PER_BLOCK for that, but if your tx then uses (4:8) for example, you'd
    // allocate much more memory every time for a new RX block than needed.
    public RxBlock(int maxNFragmentsPerBlock, ulong blockIdx1)
    {
        _blockIdx = blockIdx1;

        _fragmentMap = new bool[maxNFragmentsPerBlock]; //after creation of the RxBlock every f. is marked as unavailable
        _fragmentMap.AsSpan().Fill(FecDecodeImpl.FRAGMENT_STATUS_UNAVAILABLE);

        _blockBuffer = new List<byte[]>(maxNFragmentsPerBlock);
        for (int i = 0; i < maxNFragmentsPerBlock; i++)
        {
            _blockBuffer.Add(new byte[FecConsts.MAX_PAYLOAD_BEFORE_FEC]);
        }

        //assert(fragment_map.size() == blockBuffer.size());
    }

    //// two blocks are the same if they refer to the same block idx:
    //constexpr bool operator==(const RxBlock &other) const {
    //    return blockIdx == other.blockIdx;
    //}

    //// same for not equal operator
    //constexpr bool operator!=(const RxBlock &other) const {
    //    return !(*this == other);
    //}



    // returns true if this fragment has been already received
    public bool HasFragment(int fragmentIdx)
    {
        //assert(fragment_idx < fragment_map.size());
        return _fragmentMap[fragmentIdx] == FecDecodeImpl.FRAGMENT_STATUS_AVAILABLE;
    }

    // returns true if we are "done with this block" aka all data has been already forwarded
    public bool AllPrimaryFragmentsHaveBeenForwarded()
    {
        if (_primaryFragmentsInBlock == -1) return false;
        return _alreadyForwardedPrimaryFragments == _primaryFragmentsInBlock;
    }

    // returns true if enough FEC secondary fragments are available to replace all missing primary fragments
    public bool AllPrimaryFragmentsCanBeRecovered()
    {
        // return false if k is not known for this block yet (which means we didn't get a secondary fragment yet,
        // since each secondary fragment contains k)
        if (_primaryFragmentsInBlock == -1)
        {
            return false;
        }

        // ready for FEC step if we have as many secondary fragments as we are missing on primary fragments
        if (_availablePrimaryFragments + _availableSecondaryFragments >= _primaryFragmentsInBlock)
        {
            return true;
        }

        return false;
    }

    // returns true as soon as all primary fragments are available
    public bool AllPrimaryFragmentsAreAvailable()
    {
        if (_primaryFragmentsInBlock == -1)
        {
            return false;
        }

        return _availablePrimaryFragments == _primaryFragmentsInBlock;
    }

    // copy the fragment data and mark it as available
    // you should check if it is already available with hasFragment() to avoid copying the same fragment multiple times
    // when using multiple RX cards
    public void AddFragment(ReadOnlySpan<byte> data)
    {
        var header = FecPayloadHelper.CreateFromArray(data);

        //assert(!hasFragment(header.fragment_idx));
        //assert(header.block_idx == blockIdx);
        //assert(fragment_map[header.fragment_idx] == FRAGMENT_STATUS_UNAVAILABLE);
        //assert(header.fragment_idx < blockBuffer.size());
        fragment_copy_payload(header.fragment_idx, data);
        // mark it as available
        _fragmentMap[header.fragment_idx] = FecDecodeImpl.FRAGMENT_STATUS_AVAILABLE;

        // each fragment inside a block should report the same n of primary fragments
        if (_primaryFragmentsInBlock == -1)
        {
            _primaryFragmentsInBlock = header.n_primary_fragments;
        }
        else
        {
            //assert(m_n_primary_fragments_in_block == header.n_primary_fragments);
        }

        bool isPrimaryFragment = header.fragment_idx < header.n_primary_fragments;
        if (isPrimaryFragment)
        {
            _availablePrimaryFragments++;
        }
        else
        {
            _availableSecondaryFragments++;
            //var payload_len_including_size = dataLen - sizeof(FECPayloadHdr) + sizeof(UInt16);
            var payloadLenIncludingSize = data.Length - 8 + sizeof(ushort);
            // all secondary fragments shall have the same size
            if (_sizeOfSecondaryFragments == -1)
            {
                _sizeOfSecondaryFragments = payloadLenIncludingSize;
            }
            else
            {
                //assert(m_size_of_secondary_fragments == payload_len_including_size);
            }
        }

        if (_firstFragmentTimePoint == null)
        {
            _firstFragmentTimePoint = DateTime.Now;
        }
    }

    // util to copy the packet size and payload (and not more)
    private void fragment_copy_payload(int fragmentIdx, ReadOnlySpan<byte> data)
    {
        var buff = _blockBuffer[fragmentIdx];
        Debug.Assert(data.Slice(6).Length <= buff.Length);
        //data.AsSpan(sizeof(FECPayloadHdr) - sizeof(UInt16))
        data.Slice(6).CopyTo(buff);
    }

    /**
   * @returns the indices for all primary fragments that have not yet been forwarded and are available (already received or reconstructed).
   * Once an index is returned here, it won't be returned again
   * (Therefore, as long as you immediately forward all primary fragments returned here,everything happens in order)
   * @param discardMissingPackets : if true, gaps are ignored and fragments are forwarded even though this means the missing ones are irreversible lost
   * Be carefully with this param, use it only before you need to get rid of a block */
    public List<ushort> PullAvailablePrimaryFragments(bool discardMissingPackets)
    {
        // note: when pulling the available fragments, we do not need to know how many primary fragments this block actually contains
        List<ushort> ret = new();
        for (ushort i = (ushort)_alreadyForwardedPrimaryFragments; i < _availablePrimaryFragments; i++)
        {
            if (_fragmentMap[i] == FecDecodeImpl.FRAGMENT_STATUS_UNAVAILABLE)
            {
                if (discardMissingPackets)
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            ret.Add(i);
        }
        // make sure these indices won't be returned again
        _alreadyForwardedPrimaryFragments += ret.Count;
        return ret;
    }

    public Span<byte> get_primary_fragment_data_p(int fragmentIndex)
    {
        //assert(fragment_map[fragment_index] == FRAGMENT_STATUS_AVAILABLE);
        //assert(m_n_primary_fragments_in_block != -1);
        //assert(fragment_index < m_n_primary_fragments_in_block);
        //return blockBuffer[fragment_index].data()+sizeof(FECPayloadHdr);


        // return blockBuffer[fragment_index].data() + sizeof(uint16_t);
        return _blockBuffer[fragmentIndex].AsSpan(2);
    }

    public int get_primary_fragment_data_size(int fragmentIndex)
    {
        //assert(fragment_map[fragment_index] == FRAGMENT_STATUS_AVAILABLE);
        //assert(m_n_primary_fragments_in_block != -1);
        //assert(fragment_index < m_n_primary_fragments_in_block);
        var lenP = BinaryPrimitives.ReadUInt16LittleEndian(_blockBuffer[fragmentIndex].AsSpan(0,2));
        return lenP;
    }


    // returns the n of primary and secondary fragments for this block
    int GetNAvailableFragments()
    {
        return _availablePrimaryFragments + _availableSecondaryFragments;
    }

    /**
     * Reconstruct all missing primary fragments (data packets) by using the received secondary (FEC) packets
     * NOTE: reconstructing only part of the missing data is not supported ! (That's a non-fixable technical detail of FEC)
     * NOTE: Do not call this method unless it is needed
     * @return the n of reconstructed packets
     */
    public int ReconstructAllMissingData()
    {
        //wifibroadcast::log::get_default()->debug("reconstructAllMissingData"<<nAvailablePrimaryFragments<<" "<<nAvailableSecondaryFragments<<" "<<fec.FEC_K<<"\n";
        // NOTE: FEC does only work if nPrimaryFragments+nSecondaryFragments>=FEC_K
        //assert(m_n_primary_fragments_in_block != -1);
        //assert(m_size_of_secondary_fragments != -1);
        // do not reconstruct if reconstruction is impossible
        //assert(getNAvailableFragments() >= m_n_primary_fragments_in_block);
        // also do not reconstruct if reconstruction is not needed
        // const int nMissingPrimaryFragments = m_n_primary_fragments_in_block- m_n_available_primary_fragments;
        var recoveredFragmentIndices = FecDecodeImpl.fecDecode(_blockBuffer, _primaryFragmentsInBlock, _fragmentMap);
        // now mark them as available
        foreach (var idx in recoveredFragmentIndices)
        {
            _fragmentMap[idx] = FecDecodeImpl.FRAGMENT_STATUS_AVAILABLE;
        }

        _availablePrimaryFragments += recoveredFragmentIndices.Count;
        // n of reconstructed packets
        return recoveredFragmentIndices.Count;
    }


    public ulong GetBlockIdx()
    {
        return _blockIdx;
    }

    DateTime? GetFirstFragmentTimePoint()
    {
        return _firstFragmentTimePoint;
    }

    // Returns the number of missing primary packets (e.g. the n of actual data packets that are missing)
    // This only works if we know the "fec_k" parameter
    int? get_missing_primary_packets()
    {
        if (_primaryFragmentsInBlock <= 0)
        {
            return null;
        }

        return _primaryFragmentsInBlock - GetNAvailableFragments();
    }

    public string get_missing_primary_packets_readable()
    {
        var tmp = get_missing_primary_packets();
        if (tmp == null)
        {
            return "?";
        }
        return tmp.ToString();
    }

    int get_n_primary_fragments()
    {
        return _primaryFragmentsInBlock;
    }
}