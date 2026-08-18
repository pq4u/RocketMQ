using RocketMQ.Benchmark;

namespace RocketMQ.Benchmark.Tests;

public sealed class BenchmarkOptionsTests
{
    [Fact]
    public void Parse_UsesDefaultsForDirectScenario()
    {
        var options = BenchmarkOptions.Parse(["--endpoint", "http://localhost:50051", "--database-path", "D:\\bench\\broker.db"]);

        Assert.Equal(TimeSpan.FromMinutes(15), options.Duration);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Warmup);
        Assert.Equal(32, options.Workers);
        Assert.Equal(1024, options.PayloadBytes);
        Assert.Equal(RoutingMode.Direct, options.Routing);
        Assert.Equal(1, options.QueueCount);
        Assert.False(options.DetailedTimings);
    }

    [Fact]
    public void Parse_RejectsDirectScenarioWithMultipleQueues()
    {
        var exception = Assert.Throws<ArgumentException>(() => BenchmarkOptions.Parse([
            "--endpoint", "http://localhost:50051",
            "--database-path", "D:\\bench\\broker.db",
            "--queue-count", "2"]));

        Assert.Contains("queue-count", exception.Message);
    }

    [Fact]
    public void Parse_AcceptsFanoutScenario()
    {
        var options = BenchmarkOptions.Parse([
            "--endpoint", "http://localhost:50051",
            "--database-path", "D:\\bench\\broker.db",
            "--routing", "fanout",
            "--queue-count", "3"]);

        Assert.Equal(RoutingMode.Fanout, options.Routing);
        Assert.Equal(3, options.QueueCount);
    }

    [Fact]
    public void Parse_AcceptsDetailedTimings()
    {
        var options = BenchmarkOptions.Parse([
            "--endpoint", "http://localhost:50051",
            "--database-path", "D:\\bench\\broker.db",
            "--detailed-timings", "true"]);

        Assert.True(options.DetailedTimings);
    }
}
