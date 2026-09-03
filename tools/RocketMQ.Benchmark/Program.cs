using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Benchmark;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = BenchmarkOptions.Parse(args);
            var report = await new BenchmarkRunner(options).RunAsync(CancellationToken.None);
            var resultsDirectory = Path.GetFullPath(options.ResultsDirectory);
            Directory.CreateDirectory(resultsDirectory);
            var reportPath = Path.Combine(resultsDirectory, $"{report.RunId}.json");
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            PrintSummary(report, reportPath);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Benchmark cancelled.");
            return 2;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Benchmark failed: {exception.Message}");
            return 1;
        }
    }

    private static void PrintSummary(BenchmarkReport report, string reportPath)
    {
        Console.WriteLine($"Run: {report.RunId}");
        Console.WriteLine($"Accepted: {report.Counts.Accepted:N0}/{report.Counts.Attempts:N0}; throughput: {report.ThroughputPerSecond:N2} publish/s");
        Console.WriteLine($"Latency ms: p50={report.Latency.P50Milliseconds:N2}, p95={report.Latency.P95Milliseconds:N2}, p99={report.Latency.P99Milliseconds:N2}, max={report.Latency.MaxMilliseconds:N2}");
        if (report.DetailedTimings is { } timings)
        {
            Console.WriteLine($"Batch: mean-size={timings.BatchSize.Mean:N2}, p50-size={timings.BatchSize.P50:N0}, p95-size={timings.BatchSize.P95:N0}, assembly-mean={timings.BatchAssembly.MeanMilliseconds:N2} ms");
            Console.WriteLine($"Mean timing ms: server={timings.ServerTotal.MeanMilliseconds:N2}, writer-wait={timings.WriterWait.MeanMilliseconds:N2}, work={timings.TransactionWork.MeanMilliseconds:N2}, commit={timings.TransactionCommit.MeanMilliseconds:N2}, client/transport={timings.ClientAndTransport.MeanMilliseconds:N2}");
            Console.WriteLine($"Mean SQL work ms: cleanup={timings.Cleanup.MeanMilliseconds:N2}, fingerprint={timings.Fingerprint.MeanMilliseconds:N2}, idempotency={timings.IdempotencyLookup.MeanMilliseconds:N2}, exchange={timings.ExchangeLookup.MeanMilliseconds:N2}, routing={timings.Routing.MeanMilliseconds:N2}, publication={timings.PublicationInsert.MeanMilliseconds:N2}, enqueue={timings.Enqueue.MeanMilliseconds:N2}");
        }
        Console.WriteLine($"Storage bytes: db={report.StorageAfter.DatabaseBytes:N0}, wal={report.StorageAfter.WalBytes:N0}, shm={report.StorageAfter.ShmBytes:N0}");
        Console.WriteLine($"Report: {reportPath}");
    }
}

public sealed class BenchmarkRunner
{
    private readonly BenchmarkOptions _options;

    public BenchmarkRunner(BenchmarkOptions options) => _options = options;

    public async Task<BenchmarkReport> RunAsync(CancellationToken ct)
    {
        if (!File.Exists(_options.DatabasePath))
        {
            throw new ArgumentException("--database-path must point to the SQLite database created by the running broker.");
        }

        var runId = $"bench-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var exchangeName = $"{runId}.exchange";
        var queueNames = Enumerable.Range(1, _options.QueueCount).Select(index => $"{runId}.queue.{index}").ToArray();
        var storageBefore = CaptureStorage(_options.DatabasePath);
        using var channel = GrpcChannel.ForAddress(_options.Endpoint);
        var admin = new Admin.AdminClient(channel);
        var producer = new Producer.ProducerClient(channel);
        await DeclareTopologyAsync(admin, exchangeName, queueNames, ct);

        var payload = CreatePayload(_options.PayloadBytes);
        await SendForAsync(producer, exchangeName, payload, _options.Warmup, measure: null, ct);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var measurement = new Measurement();
        await SendForAsync(producer, exchangeName, payload, _options.Duration, measurement, ct);
        var completedAtUtc = DateTimeOffset.UtcNow;
        var durationSeconds = Math.Max((completedAtUtc - startedAtUtc).TotalSeconds, double.Epsilon);
        var storageAfter = CaptureStorage(_options.DatabasePath);
        var counts = measurement.Counts();

        return new BenchmarkReport(
            runId,
            startedAtUtc,
            completedAtUtc,
            _options.Endpoint.ToString(),
            _options.DatabasePath,
            new BenchmarkScenario(_options.Routing.ToString(), _options.QueueCount, _options.Workers, _options.PayloadBytes, _options.Warmup, _options.Duration, _options.DetailedTimings, exchangeName, queueNames),
            counts,
            counts.Accepted / durationSeconds,
            LatencyStatistics.FromTicks(measurement.Latencies),
            measurement.BuildTimings(),
            measurement.Errors.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value),
            storageBefore,
            storageAfter,
            BenchmarkEnvironment.Capture());
    }

    private async Task DeclareTopologyAsync(Admin.AdminClient admin, string exchangeName, IReadOnlyList<string> queueNames, CancellationToken ct)
    {
        var exchangeType = _options.Routing == RoutingMode.Direct ? "direct" : "fanout";
        await admin.DeclareExchangeAsync(new DeclareExchangeRequest { ExchangeName = exchangeName, ExchangeType = exchangeType }, cancellationToken: ct);
        foreach (var queueName in queueNames)
        {
            await admin.DeclareQueueAsync(new DeclareQueueRequest { QueueName = queueName }, cancellationToken: ct);
            await admin.BindAsync(new BindRequest { ExchangeName = exchangeName, QueueName = queueName, RoutingKey = _options.Routing == RoutingMode.Direct ? "benchmark" : string.Empty }, cancellationToken: ct);
        }
    }

    private async Task SendForAsync(Producer.ProducerClient producer, string exchangeName, ByteString payload, TimeSpan duration, Measurement? measure, CancellationToken ct)
    {
        if (duration == TimeSpan.Zero)
        {
            return;
        }

        using var durationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        durationCts.CancelAfter(duration);
        var tasks = Enumerable.Range(0, _options.Workers)
            .Select(_ => SendWorkerAsync(producer, exchangeName, payload, measure, durationCts.Token))
            .ToArray();
        await Task.WhenAll(tasks);
    }

    private async Task SendWorkerAsync(Producer.ProducerClient producer, string exchangeName, ByteString payload, Measurement? measure, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                var response = await producer.PublishAsync(new PublishRequest
                {
                    ExchangeName = exchangeName,
                    RoutingKey = _options.Routing == RoutingMode.Direct ? "benchmark" : string.Empty,
                    Payload = payload,
                    CorrelationId = Guid.NewGuid().ToString(),
                    PublishId = Guid.NewGuid().ToString(),
                    IncludeDiagnostics = measure is not null && _options.DetailedTimings
                }, cancellationToken: ct);
                measure?.RecordResponse(response, Stopwatch.GetTimestamp() - started);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (RpcException exception)
            {
                measure?.RecordError(exception.StatusCode.ToString(), Stopwatch.GetTimestamp() - started);
            }
            catch (Exception exception)
            {
                measure?.RecordError(exception.GetType().Name, Stopwatch.GetTimestamp() - started);
            }
        }
    }

    private static ByteString CreatePayload(int length)
    {
        var payload = new byte[length];
        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index % 251);
        }

        return ByteString.CopyFrom(payload);
    }

    private static StorageSnapshot CaptureStorage(string databasePath)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(databasePath));
        var drive = root is null ? null : new DriveInfo(root);
        return new StorageSnapshot(FileLength(databasePath), FileLength(databasePath + "-wal"), FileLength(databasePath + "-shm"), drive?.AvailableFreeSpace ?? 0);
    }

    private static long FileLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    private sealed class Measurement
    {
        private long _attempts;
        private long _accepted;
        private long _unroutable;
        private long _failed;
        private readonly TimingMeasurement _timings = new();

        public ConcurrentBag<long> Latencies { get; } = [];
        public ConcurrentDictionary<string, long> Errors { get; } = new(StringComparer.Ordinal);

        public void RecordResponse(PublishResponse response, long elapsedTicks)
        {
            Interlocked.Increment(ref _attempts);
            Latencies.Add(elapsedTicks);
            if (response.Diagnostics is not null)
            {
                _timings.Record(response.Diagnostics, elapsedTicks);
            }
            if (StringComparer.Ordinal.Equals(response.Status, "Accepted"))
            {
                Interlocked.Increment(ref _accepted);
            }
            else
            {
                Interlocked.Increment(ref _unroutable);
            }
        }

        public void RecordError(string category, long elapsedTicks)
        {
            Interlocked.Increment(ref _attempts);
            Interlocked.Increment(ref _failed);
            Latencies.Add(elapsedTicks);
            Errors.AddOrUpdate(category, 1, static (_, count) => count + 1);
        }

        public BenchmarkCounts Counts() => new(_attempts, _accepted, _unroutable, _failed);
        public PublishTimingBreakdown? BuildTimings() => _timings.Build();
    }

    private sealed class TimingMeasurement
    {
        private long _count;
        private readonly ConcurrentBag<double> _serverTotal = [];
        private readonly ConcurrentBag<double> _batchSize = [];
        private readonly ConcurrentBag<double> _batchAssembly = [];
        private readonly ConcurrentBag<double> _writerWait = [];
        private readonly ConcurrentBag<double> _connectionOpen = [];
        private readonly ConcurrentBag<double> _transactionBegin = [];
        private readonly ConcurrentBag<double> _transactionWork = [];
        private readonly ConcurrentBag<double> _transactionCommit = [];
        private readonly ConcurrentBag<double> _cleanup = [];
        private readonly ConcurrentBag<double> _fingerprint = [];
        private readonly ConcurrentBag<double> _idempotencyLookup = [];
        private readonly ConcurrentBag<double> _exchangeLookup = [];
        private readonly ConcurrentBag<double> _routing = [];
        private readonly ConcurrentBag<double> _publicationInsert = [];
        private readonly ConcurrentBag<double> _enqueue = [];
        private readonly ConcurrentBag<double> _resultRead = [];
        private readonly ConcurrentBag<double> _clientAndTransport = [];

        public void Record(PublishDiagnostics timing, long elapsedTicks)
        {
            Interlocked.Increment(ref _count);
            _serverTotal.Add(timing.ServerTotalMs);
            _batchSize.Add(timing.BatchSize);
            _batchAssembly.Add(timing.BatchAssemblyMs);
            _writerWait.Add(timing.WriterWaitMs);
            _connectionOpen.Add(timing.ConnectionOpenMs);
            _transactionBegin.Add(timing.TransactionBeginMs);
            _transactionWork.Add(timing.TransactionWorkMs);
            _transactionCommit.Add(timing.TransactionCommitMs);
            _cleanup.Add(timing.CleanupMs);
            _fingerprint.Add(timing.FingerprintMs);
            _idempotencyLookup.Add(timing.IdempotencyLookupMs);
            _exchangeLookup.Add(timing.ExchangeLookupMs);
            _routing.Add(timing.RoutingMs);
            _publicationInsert.Add(timing.PublicationInsertMs);
            _enqueue.Add(timing.EnqueueMs);
            _resultRead.Add(timing.ResultReadMs);
            var endToEndMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            _clientAndTransport.Add(Math.Max(0, endToEndMilliseconds - timing.ServerTotalMs));
        }

        public PublishTimingBreakdown? Build()
            => Interlocked.Read(ref _count) == 0
                ? null
                : new PublishTimingBreakdown(
                    LatencyStatistics.FromMilliseconds(_serverTotal),
                    NumericStatistics.FromValues(_batchSize),
                    LatencyStatistics.FromMilliseconds(_batchAssembly),
                    LatencyStatistics.FromMilliseconds(_writerWait),
                    LatencyStatistics.FromMilliseconds(_connectionOpen),
                    LatencyStatistics.FromMilliseconds(_transactionBegin),
                    LatencyStatistics.FromMilliseconds(_transactionWork),
                    LatencyStatistics.FromMilliseconds(_transactionCommit),
                    LatencyStatistics.FromMilliseconds(_cleanup),
                    LatencyStatistics.FromMilliseconds(_fingerprint),
                    LatencyStatistics.FromMilliseconds(_idempotencyLookup),
                    LatencyStatistics.FromMilliseconds(_exchangeLookup),
                    LatencyStatistics.FromMilliseconds(_routing),
                    LatencyStatistics.FromMilliseconds(_publicationInsert),
                    LatencyStatistics.FromMilliseconds(_enqueue),
                    LatencyStatistics.FromMilliseconds(_resultRead),
                    LatencyStatistics.FromMilliseconds(_clientAndTransport));
    }
}

