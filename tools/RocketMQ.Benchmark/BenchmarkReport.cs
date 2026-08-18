using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RocketMQ.Benchmark;

public sealed record StorageSnapshot(long DatabaseBytes, long WalBytes, long ShmBytes, long AvailableDiskBytes);

public sealed record LatencyStatistics(long Count, double MinMilliseconds, double P50Milliseconds, double P95Milliseconds, double P99Milliseconds, double MaxMilliseconds)
{
    public static LatencyStatistics FromTicks(IReadOnlyCollection<long> ticks)
    {
        if (ticks.Count == 0)
        {
            return new LatencyStatistics(0, 0, 0, 0, 0, 0);
        }

        var sorted = ticks.Order().ToArray();
        return new LatencyStatistics(
            sorted.Length,
            ToMilliseconds(sorted[0]),
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            Percentile(sorted, 0.99),
            ToMilliseconds(sorted[^1]));
    }

    private static double Percentile(IReadOnlyList<long> values, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * values.Count) - 1;
        return ToMilliseconds(values[Math.Clamp(index, 0, values.Count - 1)]);
    }

    private static double ToMilliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;
}

public sealed record BenchmarkReport(
    string RunId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Endpoint,
    string DatabasePath,
    BenchmarkScenario Scenario,
    BenchmarkCounts Counts,
    double ThroughputPerSecond,
    LatencyStatistics Latency,
    IReadOnlyDictionary<string, long> Errors,
    StorageSnapshot StorageBefore,
    StorageSnapshot StorageAfter,
    BenchmarkEnvironment Environment);

public sealed record BenchmarkScenario(
    string Routing,
    int QueueCount,
    int Workers,
    int PayloadBytes,
    TimeSpan Warmup,
    TimeSpan Duration,
    string ExchangeName,
    IReadOnlyList<string> QueueNames);

public sealed record BenchmarkCounts(long Attempts, long Accepted, long Unroutable, long Failed);

public sealed record BenchmarkEnvironment(
    string OperatingSystem,
    string Framework,
    string ProcessArchitecture,
    int ProcessorCount,
    string MachineName)
{
    public static BenchmarkEnvironment Capture() => new(
        RuntimeInformation.OSDescription,
        RuntimeInformation.FrameworkDescription,
        RuntimeInformation.ProcessArchitecture.ToString(),
        Environment.ProcessorCount,
        Environment.MachineName);
}

