using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006

namespace WiFiBroadcastNet.RadioStreams;

// This encapsulates everything you need when working on a single FEC block on the receiver
// for example, addFragment() or pullAvailablePrimaryFragments()
// it also provides convenient methods to query if the block is fully forwarded
// or if it is ready for the FEC reconstruction step.
class RxBlock
{
    private const int MAX_PAYLOAD_BEFORE_FEC = 1449;

    /// <summary>
    /// the block idx marks which block this element refers to
    /// </summary>
    private readonly UInt64 blockIdx = 0;

    /// <summary>
    /// for each fragment (via fragment_idx) store if it has been received yet
    /// </summary>
    private readonly List<bool> fragment_map;

    /// <summary>
    /// holds all the data for all received fragments (if fragment_map says UNAVALIABLE at this position, content is undefined)
    /// </summary>
    private readonly List<byte[]> blockBuffer;

    /// <summary>
    /// n of primary fragments that are already pulled out
    /// </summary>
    private int nAlreadyForwardedPrimaryFragments = 0;

    // time point when the first fragment for this block was received (via addFragment() )
    private DateTime? firstFragmentTimePoint = null;
    // as soon as we know any of the fragments for this block, we know how many primary fragments this block contains
    // (and therefore, how many primary or secondary fragments we need to fully reconstruct)
    int m_n_primary_fragments_in_block = -1;
    // for the fec step, we need the size of the fec secondary fragments, which should be equal for all secondary fragments
    int m_size_of_secondary_fragments = -1;
    int m_n_available_primary_fragments = 0;
    int m_n_available_secondary_fragments = 0;


    // @param maxNFragmentsPerBlock max number of primary and secondary fragments for this block.
    // you could just use MAX_TOTAL_FRAGMENTS_PER_BLOCK for that, but if your tx then uses (4:8) for example, you'd
    // allocate much more memory every time for a new RX block than needed.
    public RxBlock(int maxNFragmentsPerBlock, UInt64 blockIdx1)
    {
        blockIdx = blockIdx1;
        fragment_map = new List<bool>(maxNFragmentsPerBlock); //after creation of the RxBlock every f. is marked as unavailable
        for (int i = 0; i < maxNFragmentsPerBlock; i++)
        {
            fragment_map[i] = FecDecodeImpl.FRAGMENT_STATUS_UNAVAILABLE;
        }

        blockBuffer = new List<byte[]>(maxNFragmentsPerBlock);

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
    public bool hasFragment(int fragment_idx)
    {
        //assert(fragment_idx < fragment_map.size());
        return fragment_map[fragment_idx] == FecDecodeImpl.FRAGMENT_STATUS_AVAILABLE;
    }

    // returns true if we are "done with this block" aka all data has been already forwarded
    public bool allPrimaryFragmentsHaveBeenForwarded()
    {
        if (m_n_primary_fragments_in_block == -1)return false;
        return nAlreadyForwardedPrimaryFragments == m_n_primary_fragments_in_block;
    }

    // returns true if enough FEC secondary fragments are available to replace all missing primary fragments
    public bool allPrimaryFragmentsCanBeRecovered()
    {
        // return false if k is not known for this block yet (which means we didn't get a secondary fragment yet,
        // since each secondary fragment contains k)
        if (m_n_primary_fragments_in_block == -1)
        {
            return false;
        }

        // ready for FEC step if we have as many secondary fragments as we are missing on primary fragments
        if (m_n_available_primary_fragments + m_n_available_secondary_fragments >= m_n_primary_fragments_in_block)
        {
            return true;
        }

        return false;
    }

    // returns true as soon as all primary fragments are available
    public bool allPrimaryFragmentsAreAvailable()
    {
        if (m_n_primary_fragments_in_block == -1)
        {
            return false;
        }

        return m_n_available_primary_fragments == m_n_primary_fragments_in_block;
    }

    // copy the fragment data and mark it as available
    // you should check if it is already available with hasFragment() to avoid copying the same fragment multiple times
    // when using multiple RX cards
    public void addFragment(byte[] data, int dataLen)
    {
        var header = MemoryMarshal.Read<FECPayloadHdr>(data.AsSpan(0, 8));

        //assert(!hasFragment(header.fragment_idx));
        //assert(header.block_idx == blockIdx);
        //assert(fragment_map[header.fragment_idx] == FRAGMENT_STATUS_UNAVAILABLE);
        //assert(header.fragment_idx < blockBuffer.size());
        fragment_copy_payload(header.fragment_idx, data, dataLen);
        // mark it as available
        fragment_map[header.fragment_idx] = FecDecodeImpl.FRAGMENT_STATUS_AVAILABLE;

        // each fragment inside a block should report the same n of primary fragments
        if (m_n_primary_fragments_in_block == -1)
        {
            m_n_primary_fragments_in_block = header.n_primary_fragments;
        }
        else
        {
            //assert(m_n_primary_fragments_in_block == header.n_primary_fragments);
        }

        bool is_primary_fragment = header.fragment_idx < header.n_primary_fragments;
        if (is_primary_fragment)
        {
            m_n_available_primary_fragments++;
        }
        else
        {
            m_n_available_secondary_fragments++;
            //var payload_len_including_size = dataLen - sizeof(FECPayloadHdr) + sizeof(UInt16);
            var payload_len_including_size = dataLen - 8 + sizeof(UInt16);
            // all secondary fragments shall have the same size
            if (m_size_of_secondary_fragments == -1)
            {
                m_size_of_secondary_fragments = payload_len_including_size;
            }
            else
            {
                //assert(m_size_of_secondary_fragments == payload_len_including_size);
            }
        }

        if (firstFragmentTimePoint == null)
        {
            firstFragmentTimePoint = DateTime.Now;
        }
    }

    // util to copy the packet size and payload (and not more)
    public void fragment_copy_payload(int fragment_idx, byte[] data, int dataLen)
    {
        var buff = new byte[MAX_PAYLOAD_BEFORE_FEC];
        blockBuffer[fragment_idx] = buff;

        //data.AsSpan(sizeof(FECPayloadHdr) - sizeof(UInt16))
        data.AsSpan(6..dataLen).CopyTo(buff);
    }

    /**
   * @returns the indices for all primary fragments that have not yet been forwarded and are available (already received or reconstructed).
   * Once an index is returned here, it won't be returned again
   * (Therefore, as long as you immediately forward all primary fragments returned here,everything happens in order)
   * @param discardMissingPackets : if true, gaps are ignored and fragments are forwarded even though this means the missing ones are irreversible lost
   * Be carefully with this param, use it only before you need to get rid of a block */
    List<UInt16> pullAvailablePrimaryFragments(bool discardMissingPackets)
    {
        // note: when pulling the available fragments, we do not need to know how many primary fragments this block actually contains
        List<UInt16> ret = new();
        for (UInt16 i = (UInt16)nAlreadyForwardedPrimaryFragments; i < m_n_available_primary_fragments; i++)
        {
            if (fragment_map[i] == FecDecodeImpl.FRAGMENT_STATUS_UNAVAILABLE)
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
        nAlreadyForwardedPrimaryFragments += (int) ret.Count;
        return ret;
    }

    byte[] get_primary_fragment_data_p(int fragment_index)
    {
        //assert(fragment_map[fragment_index] == FRAGMENT_STATUS_AVAILABLE);
        //assert(m_n_primary_fragments_in_block != -1);
        //assert(fragment_index < m_n_primary_fragments_in_block);
        //return blockBuffer[fragment_index].data()+sizeof(FECPayloadHdr);


        // return blockBuffer[fragment_index].data() + sizeof(uint16_t);
        return blockBuffer[fragment_index];
    }

    int get_primary_fragment_data_size(int fragment_index)
    {
        //assert(fragment_map[fragment_index] == FRAGMENT_STATUS_AVAILABLE);
        //assert(m_n_primary_fragments_in_block != -1);
        //assert(fragment_index < m_n_primary_fragments_in_block);
        var len_p = blockBuffer[fragment_index].Length;
        return len_p;
    }


    // returns the n of primary and secondary fragments for this block
    int getNAvailableFragments()
    {
        return m_n_available_primary_fragments + m_n_available_secondary_fragments;
    }

    /**
     * Reconstruct all missing primary fragments (data packets) by using the received secondary (FEC) packets
     * NOTE: reconstructing only part of the missing data is not supported ! (That's a non-fixable technical detail of FEC)
     * NOTE: Do not call this method unless it is needed
     * @return the n of reconstructed packets
     */
    int reconstructAllMissingData()
    {
        //wifibroadcast::log::get_default()->debug("reconstructAllMissingData"<<nAvailablePrimaryFragments<<" "<<nAvailableSecondaryFragments<<" "<<fec.FEC_K<<"\n";
        // NOTE: FEC does only work if nPrimaryFragments+nSecondaryFragments>=FEC_K
        //assert(m_n_primary_fragments_in_block != -1);
        //assert(m_size_of_secondary_fragments != -1);
        // do not reconstruct if reconstruction is impossible
        //assert(getNAvailableFragments() >= m_n_primary_fragments_in_block);
        // also do not reconstruct if reconstruction is not needed
        // const int nMissingPrimaryFragments = m_n_primary_fragments_in_block- m_n_available_primary_fragments;
        var recoveredFragmentIndices = FecDecodeImpl.fecDecode(m_size_of_secondary_fragments, blockBuffer,
                                                  m_n_primary_fragments_in_block, fragment_map);
        // now mark them as available
        foreach (var idx in recoveredFragmentIndices)
        {
            fragment_map[idx] = FecDecodeImpl.FRAGMENT_STATUS_AVAILABLE;
        }

        m_n_available_primary_fragments += recoveredFragmentIndices.Count;
        // n of reconstructed packets
        return recoveredFragmentIndices.Count;
    }


    UInt64 getBlockIdx()
    {
        return blockIdx;
    }

    DateTime? getFirstFragmentTimePoint()
    {
        return firstFragmentTimePoint;
    }

    // Returns the number of missing primary packets (e.g. the n of actual data packets that are missing)
    // This only works if we know the "fec_k" parameter
    int? get_missing_primary_packets()
    {
        if (m_n_primary_fragments_in_block <= 0)
        {
            return null;
        }

        return m_n_primary_fragments_in_block - getNAvailableFragments();
    }

    string get_missing_primary_packets_readable()
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
        return m_n_primary_fragments_in_block;
    }
}