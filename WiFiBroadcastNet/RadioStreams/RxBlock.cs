using System;

namespace WiFiBroadcastNet.RadioStreams;

// This encapsulates everything you need when working on a single FEC block on the receiver
// for example, addFragment() or pullAvailablePrimaryFragments()
// it also provides convenient methods to query if the block is fully forwarded
// or if it is ready for the FEC reconstruction step.
class RxBlock {
public:
    // @param maxNFragmentsPerBlock max number of primary and secondary fragments for this block.
    // you could just use MAX_TOTAL_FRAGMENTS_PER_BLOCK for that, but if your tx then uses (4:8) for example, you'd
    // allocate much more memory every time for a new RX block than needed.
    RxBlock(const unsigned int maxNFragmentsPerBlock, const uint64_t blockIdx1) : blockIdx(blockIdx1),
                                                                                  fragment_map(maxNFragmentsPerBlock,
                                                                                               FRAGMENT_STATUS_UNAVAILABLE), //after creation of the RxBlock every f. is marked as unavailable
                                                                                  blockBuffer(maxNFragmentsPerBlock) {
        assert(fragment_map.size() == blockBuffer.size());
    }

    // No copy constructor for safety
    RxBlock(const RxBlock &) = delete;

    // two blocks are the same if they refer to the same block idx:
    constexpr bool operator==(const RxBlock &other) const {
        return blockIdx == other.blockIdx;
    }

    // same for not equal operator
    constexpr bool operator!=(const RxBlock &other) const {
        return !(*this == other);
    }

    ~RxBlock() = default;

public:
    // returns true if this fragment has been already received
    bool hasFragment(const int fragment_idx) {
        assert(fragment_idx < fragment_map.size());
        return fragment_map[fragment_idx] == FRAGMENT_STATUS_AVAILABLE;
    }

    // returns true if we are "done with this block" aka all data has been already forwarded
    bool allPrimaryFragmentsHaveBeenForwarded() const {
        if (m_n_primary_fragments_in_block == -1)return false;
        return nAlreadyForwardedPrimaryFragments == m_n_primary_fragments_in_block;
    }

    // returns true if enough FEC secondary fragments are available to replace all missing primary fragments
    bool allPrimaryFragmentsCanBeRecovered() const {
        // return false if k is not known for this block yet (which means we didn't get a secondary fragment yet,
        // since each secondary fragment contains k)
        if (m_n_primary_fragments_in_block == -1)return false;
        // ready for FEC step if we have as many secondary fragments as we are missing on primary fragments
        if (m_n_available_primary_fragments + m_n_available_secondary_fragments >= m_n_primary_fragments_in_block)
            return true;
        return false;
    }

    // returns true as soon as all primary fragments are available
    bool allPrimaryFragmentsAreAvailable() const {
        if (m_n_primary_fragments_in_block == -1)return false;
        return m_n_available_primary_fragments == m_n_primary_fragments_in_block;
    }

    // copy the fragment data and mark it as available
    // you should check if it is already available with hasFragment() to avoid copying the same fragment multiple times
    // when using multiple RX cards
    void addFragment(const uint8_t *data, const std::size_t dataLen) {
        auto *hdr_p = (FECPayloadHdr *) data;
        FECPayloadHdr &header = *hdr_p;
        assert(!hasFragment(header.fragment_idx));
        assert(header.block_idx == blockIdx);
        assert(fragment_map[header.fragment_idx] == FRAGMENT_STATUS_UNAVAILABLE);
        assert(header.fragment_idx < blockBuffer.size());
        fragment_copy_payload(header.fragment_idx, data, dataLen);
        // mark it as available
        fragment_map[header.fragment_idx] = FRAGMENT_STATUS_AVAILABLE;

        // each fragment inside a block should report the same n of primary fragments
        if (m_n_primary_fragments_in_block == -1) {
            m_n_primary_fragments_in_block = header.n_primary_fragments;
        } else {
            assert(m_n_primary_fragments_in_block == header.n_primary_fragments);
        }
        const bool is_primary_fragment = header.fragment_idx < header.n_primary_fragments;
        if (is_primary_fragment) {
            m_n_available_primary_fragments++;
        } else {
            m_n_available_secondary_fragments++;
            const auto payload_len_including_size = dataLen - sizeof(FECPayloadHdr) + sizeof(uint16_t);
            // all secondary fragments shall have the same size
            if (m_size_of_secondary_fragments == -1) {
                m_size_of_secondary_fragments = payload_len_including_size;
            } else {
                assert(m_size_of_secondary_fragments == payload_len_including_size);
            }
        }
        if (firstFragmentTimePoint == std::nullopt) {
            firstFragmentTimePoint = std::chrono::steady_clock::now();
        }
    }

    // util to copy the packet size and payload (and not more)
    void fragment_copy_payload(const int fragment_idx, const uint8_t *data, const std::size_t dataLen) {
        uint8_t *buff = blockBuffer[fragment_idx].data();
        // NOTE: FECPayloadHdr::data_size needs to be included during the fec decode step
        const uint8_t *payload_p = data + sizeof(FECPayloadHdr) - sizeof(uint16_t);
        auto payload_s = dataLen - sizeof(FECPayloadHdr) + sizeof(uint16_t);
        // write the data (doesn't matter if FEC data or correction packet)
        memcpy(buff, payload_p, payload_s);
        // set the rest to zero such that FEC works
        memset(buff + payload_s, 0, MAX_PAYLOAD_BEFORE_FEC - payload_s);
    }

    /**
   * @returns the indices for all primary fragments that have not yet been forwarded and are available (already received or reconstructed).
   * Once an index is returned here, it won't be returned again
   * (Therefore, as long as you immediately forward all primary fragments returned here,everything happens in order)
   * @param discardMissingPackets : if true, gaps are ignored and fragments are forwarded even though this means the missing ones are irreversible lost
   * Be carefully with this param, use it only before you need to get rid of a block */
    std::vector<uint16_t> pullAvailablePrimaryFragments(const bool discardMissingPackets) {
        // note: when pulling the available fragments, we do not need to know how many primary fragments this block actually contains
        std::vector<uint16_t> ret;
        for (int i = nAlreadyForwardedPrimaryFragments; i < m_n_available_primary_fragments; i++) {
            if (fragment_map[i] == FRAGMENT_STATUS_UNAVAILABLE) {
                if (discardMissingPackets) {
                    continue;
                } else {
                    break;
                }
            }
            ret.push_back(i);
        }
        // make sure these indices won't be returned again
        nAlreadyForwardedPrimaryFragments += (int) ret.size();
        return ret;
    }

    const uint8_t *get_primary_fragment_data_p(const int fragment_index) {
        assert(fragment_map[fragment_index] == FRAGMENT_STATUS_AVAILABLE);
        assert(m_n_primary_fragments_in_block != -1);
        assert(fragment_index < m_n_primary_fragments_in_block);
        //return blockBuffer[fragment_index].data()+sizeof(FECPayloadHdr);
        return blockBuffer[fragment_index].data() + sizeof(uint16_t);
    }

    const int get_primary_fragment_data_size(const int fragment_index) {
        assert(fragment_map[fragment_index] == FRAGMENT_STATUS_AVAILABLE);
        assert(m_n_primary_fragments_in_block != -1);
        assert(fragment_index < m_n_primary_fragments_in_block);
        uint16_t *len_p = (uint16_t *) blockBuffer[fragment_index].data();
        return *len_p;
    }


    // returns the n of primary and secondary fragments for this block
    int getNAvailableFragments() const {
        return m_n_available_primary_fragments + m_n_available_secondary_fragments;
    }

    /**
     * Reconstruct all missing primary fragments (data packets) by using the received secondary (FEC) packets
     * NOTE: reconstructing only part of the missing data is not supported ! (That's a non-fixable technical detail of FEC)
     * NOTE: Do not call this method unless it is needed
     * @return the n of reconstructed packets
     */
    int reconstructAllMissingData() {
        //wifibroadcast::log::get_default()->debug("reconstructAllMissingData"<<nAvailablePrimaryFragments<<" "<<nAvailableSecondaryFragments<<" "<<fec.FEC_K<<"\n";
        // NOTE: FEC does only work if nPrimaryFragments+nSecondaryFragments>=FEC_K
        assert(m_n_primary_fragments_in_block != -1);
        assert(m_size_of_secondary_fragments != -1);
        // do not reconstruct if reconstruction is impossible
        assert(getNAvailableFragments() >= m_n_primary_fragments_in_block);
        // also do not reconstruct if reconstruction is not needed
        // const int nMissingPrimaryFragments = m_n_primary_fragments_in_block- m_n_available_primary_fragments;
        auto recoveredFragmentIndices = fecDecode(m_size_of_secondary_fragments, blockBuffer,
                                                  m_n_primary_fragments_in_block, fragment_map);
        // now mark them as available
        for (const auto idx: recoveredFragmentIndices) {
            fragment_map[idx] = FRAGMENT_STATUS_AVAILABLE;
        }
        m_n_available_primary_fragments += recoveredFragmentIndices.size();
        // n of reconstructed packets
        return recoveredFragmentIndices.size();
    }


    [[nodiscard]] uint64_t getBlockIdx() const {
        return blockIdx;
    }

    [[nodiscard]] std::optional<std::chrono::steady_clock::time_point> getFirstFragmentTimePoint() const {
        return firstFragmentTimePoint;
    }

    // Returns the number of missing primary packets (e.g. the n of actual data packets that are missing)
    // This only works if we know the "fec_k" parameter
    std::optional<int> get_missing_primary_packets() const {
        if (m_n_primary_fragments_in_block <= 0)return std::nullopt;
        return m_n_primary_fragments_in_block - getNAvailableFragments();
    }

    std::string get_missing_primary_packets_readable() const {
        const auto tmp = get_missing_primary_packets();
        if (tmp == std::nullopt)return "?";
        return std::to_string(tmp.value());
    }

    int get_n_primary_fragments() const {
        return m_n_primary_fragments_in_block;
    }

private:
    // the block idx marks which block this element refers to
    const uint64_t blockIdx = 0;
    // n of primary fragments that are already pulled out
    int nAlreadyForwardedPrimaryFragments = 0;
    // for each fragment (via fragment_idx) store if it has been received yet
    std::vector<bool> fragment_map;
    // holds all the data for all received fragments (if fragment_map says UNAVALIABLE at this position, content is undefined)
    std::vector<std::array<uint8_t, MAX_PAYLOAD_BEFORE_FEC>> blockBuffer;
    // time point when the first fragment for this block was received (via addFragment() )
    std::optional<std::chrono::steady_clock::time_point> firstFragmentTimePoint = std::nullopt;
    // as soon as we know any of the fragments for this block, we know how many primary fragments this block contains
    // (and therefore, how many primary or secondary fragments we need to fully reconstruct)
    int m_n_primary_fragments_in_block = -1;
    // for the fec step, we need the size of the fec secondary fragments, which should be equal for all secondary fragments
    int m_size_of_secondary_fragments = -1;
    int m_n_available_primary_fragments = 0;
    int m_n_available_secondary_fragments = 0;
};
