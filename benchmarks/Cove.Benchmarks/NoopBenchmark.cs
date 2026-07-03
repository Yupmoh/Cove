using BenchmarkDotNet.Attributes;

namespace Cove.Benchmarks;

public class NoopBenchmark
{
    [Benchmark]
    public int Add()
    {
        var x = 2;
        var y = 2;
        return x + y;
    }
}
