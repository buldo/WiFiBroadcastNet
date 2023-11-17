using MessagePack;

namespace OpenHd.Fec.Benchmarks;

[MessagePackObject]
public class FecTestCase
{
    [Key(1)]
    public int BlockSize { get; set; }

    [Key(2)]
    public List<byte[]> DataBlocks { get; set; }

    [Key(3)]
    public int NrDataBlocks { get; set; }

    [Key(4)]
    public List<byte[]> FecBlocks { get; set; }

    [Key(5)]
    public List<int> FecBlockNos { get; set; }

    [Key(6)]
    public List<int> ErasedBlocks { get; set; }

    [Key(7)]
    public int NrFecBlocks { get; set; }

    [Key(8)]
    public List<byte[]> DataBlocksAfterFec { get; set; }
}