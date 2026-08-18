using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RocketMQ.Benchmark;

public sealed record StorageSnapshot(long DatabaseBytes, long WalBytes, long ShmBytes, long AvailableDiskBytes);

public sealed record LatencyStatistics(long Count, double MinMilliseconds, double MeanMilliseconds, double P50Milliseconds, double P95Milliseconds, double P99Milliseconds, double MaxMilliseconds)
{
    public static LatencyStatistics FromTicks(IReadOnlyCollection<long> ticks)
        => FromMilliseconds(ticks.Select(ToMilliseconds).ToArray());

    public static LatencyStatistics FromMilliseconds(IReadOnlyCollection<double> milliseconds)
    {
        if (milliseconds.Count == 0)
        {
            return new LatencyStatistics(0, 0, 0, 0, 0, 0, 0);
        }

        var sorted = milliseconds.Order().ToArray();
        return new LatencyStatistics(
            sorted.Length,
            sorted[0],
            sorted.Average(),
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            Percentile(sorted, 0.99),
            sorted[^1]);
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * values.Count) - 1;
        return values[Math.Clamp(index, 0, values.Count - 1)];
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
    PublishTimingBreakdown? DetailedTimings,
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
    bool DetailedTimings,
    string ExchangeName,
    IReadOnlyList<string> QueueNames);

public sealed record BenchmarkCounts(long Attempts, long Accepted, long Unroutable, long Failed);

public sealed record PublishTimingBreakdown(
    LatencyStatistics ServerTotal,
    LatencyStatistics WriterWait,
    LatencyStatistics ConnectionOpen,
    LatencyStatistics TransactionBegin,
    LatencyStatistics TransactionWork,
    LatencyStatistics TransactionCommit,
    LatencyStatistics Cleanup,
    LatencyStatistics Fingerprint,
    LatencyStatistics IdempotencyLookup,
    LatencyStatistics ExchangeLookup,
    LatencyStatistics Routing,
    LatencyStatistics PublicationInsert,
    LatencyStatistics Enqueue,
    LatencyStatistics ResultRead,
    LatencyStatistics ClientAndTransport);

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

