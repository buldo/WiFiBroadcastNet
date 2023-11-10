using System;

using WiFiBroadcastNet.RadioStreams;

namespace WiFiBroadcastNet.Fec;

public class FecConsts
{
    // max 255 primary and secondary fragments together for now. Theoretically, this implementation has enough bytes in the header for
    // up to 15 bit fragment indices, 2^15=32768
    // Note: currently limited by the fec c implementation
    public const UInt16 MAX_N_P_FRAGMENTS_PER_BLOCK = 128;
    public const UInt16 MAX_N_S_FRAGMENTS_PER_BLOCK = 128;
    public const UInt16 MAX_TOTAL_FRAGMENTS_PER_BLOCK = MAX_N_P_FRAGMENTS_PER_BLOCK + MAX_N_S_FRAGMENTS_PER_BLOCK;

    public const int MAX_PAYLOAD_BEFORE_FEC = 1449;

    public const int FEC_PACKET_MAX_PAYLOAD_SIZE = MAX_PAYLOAD_BEFORE_FEC - 8;
    //public const int FEC_PACKET_MAX_PAYLOAD_SIZE = MAX_PAYLOAD_BEFORE_FEC - sizeof(FECPayloadHdr);
    //static_assert(FEC_PACKET_MAX_PAYLOAD_SIZE==1441);
}