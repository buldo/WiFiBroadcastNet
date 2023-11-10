using System.Runtime.InteropServices;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WiFiBroadcastNet.RadioStreams;

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 8)]
struct FECPayloadHdr
{
    /// <summary>
    /// Most often each frame is encoded as one fec block rolling
    /// </summary>
    [FieldOffset(0)] public UInt32 block_idx;

    /// <summary>
    /// each fragment inside a block has a fragment index
    /// uint8_t is enough, since we are limited to 128+128=256 fragments anyway by the FEC impl.
    /// </summary>
    [FieldOffset(4)] public byte fragment_idx;

    /// <summary>
    /// how many fragments make up the primary fragments part, the rest is secondary fragments
    /// note that we do not need to know how many secondary fragments have been created - as soon as we
    /// 'have enough', we can perform the FEC correction step if necessary
    /// </summary>
    [FieldOffset(5)] public byte n_primary_fragments;

    /// <summary>
    /// For FEC all data fragments have to be the same size. We pad the rest during encoding / decoding with 0,
    /// and do this when encoding / decoding such that the 0 bytes don't have to be transmitted.
    /// This needs to be included during the fec encode / decode step !
    /// </summary>
    [FieldOffset(6)] public UInt16 data_size;
}

internal static class FecPayloadHelper
{
    public static FECPayloadHdr CreateFromArray(byte[] data)
    {
        return MemoryMarshal.Read<FECPayloadHdr>(data.AsSpan(0, 8));
    }
}