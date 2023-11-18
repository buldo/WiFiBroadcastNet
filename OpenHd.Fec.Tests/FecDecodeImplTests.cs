using MessagePack;

namespace OpenHd.Fec.Tests;

public class FecDecodeImplTests
{
    private List<FecTestCase> _cases = new();

    [SetUp]
    public void Setup()
    {
        for (int i = 0; i < 100; i++)
        {
            var name = $"fec_cases/case{i:D4}.bin";
            var data = File.ReadAllBytes(name);
            var testCase = MessagePackSerializer.Deserialize<FecTestCase>(data);
            _cases.Add(testCase);
        }
    }

    [Test]
    public void Test1()
    {
        foreach (var fecTestCase in _cases)
        {
            FecDecodeImpl.fec_decode(
                fecTestCase.DataBlocks,
                fecTestCase.NrDataBlocks,
                fecTestCase.FecBlocks,
                fecTestCase.FecBlockNos,
                fecTestCase.ErasedBlocks,
                fecTestCase.NrFecBlocks);

            for (int i = 0; i < fecTestCase.DataBlocks.Count; i++)
            {
                var actual = fecTestCase.DataBlocks[i];
                var expected = fecTestCase.DataBlocksAfterFec[i];

                CollectionAssert.AreEqual(expected, actual);
            }
        }

        Assert.Pass();
    }
}