using BenchmarkDotNet.Running;

namespace EventSourcing.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<CqrsBenchmarks>();
    }
}
