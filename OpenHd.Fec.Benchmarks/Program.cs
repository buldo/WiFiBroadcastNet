using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using MessagePack;

namespace OpenHd.Fec.Benchmarks;

// Ryzen 7 2800X Baseline
// | Method     | Mean     | Error     | StdDev    |
// |----------- |---------:|----------:|----------:|
// | ComputeFec | 77.36 us | 0.308 us  | 0.273 us  |

// Ryzen 7 2800X Ssse3
// | Method     | Mean     | Error     | StdDev    |
// |----------- |---------:|----------:|----------:|
// | ComputeFec | 5.037 us | 0.0124 us | 0.0104 us |

// RPI-4 Baseline
// | Method     | Mean     | Error     | StdDev    |
// |----------- |---------:|----------:|----------:|
// | ComputeFec | 303.2 us | 0.62 us   | 0.58 us   |

// RPI-4 NEON
// | Method     | Mean     | Error     | StdDev    |
// |----------- |---------:|----------:|----------:|
// | ComputeFec | 36.89 us | 0.031 us  | 0.024 us  |

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
