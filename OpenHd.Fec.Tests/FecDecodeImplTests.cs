using MessagePack;
using NUnit.Framework.Legacy;

namespace OpenHd.Fec.Tests;

public class FecDecodeImplTests
{
    public static List<FecTestCase> Cases = new();

    static FecDecodeImplTests()
    {
        for (int i = 0; i < 100; i++)
        {
            var name = $"fec_cases/case{i:D4}.bin";
            var data = File.ReadAllBytes(name);
            var testCase = MessagePackSerializer.Deserialize<FecTestCase>(data);
            Cases.Add(testCase);
        }
    }

    [SetUp]
    public void Setup()
    {

    }

    [Test]
    [TestCaseSource(nameof(Cases))]
    public void Test1(FecTestCase fecTestCase)
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
}