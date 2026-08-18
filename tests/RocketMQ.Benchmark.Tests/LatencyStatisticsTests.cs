using System.Diagnostics;
using RocketMQ.Benchmark;

namespace RocketMQ.Benchmark.Tests;

public sealed class LatencyStatisticsTests
{
    [Fact]
    public void FromTicks_CalculatesNearestRankPercentiles()
    {
        var ticks = Enumerable.Range(1, 100).Select(index => (long)index * Stopwatch.Frequency / 1000).ToArray();

        var statistics = LatencyStatistics.FromTicks(ticks);

        Assert.Equal(100, statistics.Count);
        Assert.Equal(1, statistics.MinMilliseconds, 6);
        Assert.Equal(50, statistics.P50Milliseconds, 6);
        Assert.Equal(95, statistics.P95Milliseconds, 6);
        Assert.Equal(99, statistics.P99Milliseconds, 6);
        Assert.Equal(100, statistics.MaxMilliseconds, 6);
    }
}

