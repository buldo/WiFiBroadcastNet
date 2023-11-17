using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using MessagePack;

namespace OpenHd.Fec.Benchmarks;

// Base line
// 1a8b837b6faaf7bd004626a89f9cb8c44b66d2c6
// | Method     | Mean     | Error   | StdDev  |
// |----------- |---------:|--------:|--------:|
// | ComputeFec | 159.4 us | 1.90 us | 1.58 us |

public class Program
{
    public class FecBench
    {
        private readonly FecTestCase _testCase;

        public FecBench()
        {
            var data = File.ReadAllBytes($"fec_cases/case0084.bin");
            _testCase = MessagePackSerializer.Deserialize<FecTestCase>(data);
        }

        [Benchmark]
        public List<byte[]> ComputeFec()
        {
            FecDecodeImpl.fec_decode(
                _testCase.BlockSize,
                _testCase.DataBlocks,
                _testCase.NrDataBlocks,
                _testCase.FecBlocks,
                _testCase.FecBlockNos,
                _testCase.ErasedBlocks,
                _testCase.NrFecBlocks);

            return _testCase.DataBlocks;
        }
    }

    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<FecBench>();
    }
}
