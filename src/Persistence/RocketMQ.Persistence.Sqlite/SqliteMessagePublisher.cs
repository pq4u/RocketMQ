using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Diagnostics;
using RocketMQ.Core.Models;
using RocketMQ.Core.Routing;

namespace RocketMQ.Persistence.Sqlite;

public sealed class SqliteMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    public const int DefaultMaxBatchSize = 32;
    public static readonly TimeSpan DefaultMaxBatchDelay = TimeSpan.FromMilliseconds(1);
    public const int DefaultQueueCapacity = 1024;

    private readonly SqliteDatabase _database;
    private readonly int _maxBatchSize;
    private readonly TimeSpan _maxBatchDelay;
    private readonly Channel<PendingPublish> _requests;
    private readonly Task _worker;
    private int _disposed;

    public SqliteMessagePublisher(SqliteDatabase database)
        : this(database, DefaultMaxBatchSize, DefaultMaxBatchDelay, DefaultQueueCapacity)
    {
    }

    public SqliteMessagePublisher(
        SqliteDatabase database,
        int maxBatchSize,
        TimeSpan maxBatchDelay,
        int queueCapacity = DefaultQueueCapacity)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (maxBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBatchSize));
        }

        if (maxBatchDelay < TimeSpan.Zero || maxBatchDelay == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBatchDelay));
        }

        if (queueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }

        _database = database;
        _maxBatchSize = maxBatchSize;
        _maxBatchDelay = maxBatchDelay;
        _requests = Channel.CreateBounded<PendingPublish>(new BoundedChannelOptions(queueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        _worker = Task.Run(ProcessBatchesAsync);
    }

    public async Task<PublishResult> PublishAsync(Guid publishId, Envelope envelope, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var diagnostics = Activity.Current?.GetTagItem(PublishDiagnosticTags.Enabled) is true
            ? Activity.Current
            : null;
        var request = new PendingPublish(
            publishId,
            envelope,
            ct,
            Stopwatch.GetTimestamp(),
            diagnostics,
            new TaskCompletionSource<PublishResult>(TaskCreationOptions.RunContinuationsAsynchronously));

        try
        {
            await _requests.Writer.WriteAsync(request, ct);
        }
        catch (ChannelClosedException)
        {
            throw new ObjectDisposedException(nameof(SqliteMessagePublisher));
        }

        return await request.Completion.Task.WaitAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _requests.Writer.TryComplete();
        await _worker;
    }

    private async Task ProcessBatchesAsync()
    {
        try
        {
            while (await _requests.Reader.WaitToReadAsync())
            {
                if (!_requests.Reader.TryRead(out var first))
                {
                    continue;
                }

                var batch = new List<PendingPublish>(_maxBatchSize) { first };
                await FillBatchAsync(batch, first.EnqueuedAt);
                var assemblyMilliseconds = Stopwatch.GetElapsedTime(first.EnqueuedAt).TotalMilliseconds;
                await PersistBatchAsync(batch, assemblyMilliseconds);
            }
        }
        catch (Exception exception)
        {
            _requests.Writer.TryComplete(exception);
            while (_requests.Reader.TryRead(out var request))
            {
                request.Completion.TrySetException(exception);
            }

            throw;
        }
    }

    private async Task FillBatchAsync(List<PendingPublish> batch, long batchStartedAt)
    {
        while (batch.Count < _maxBatchSize)
        {
            while (batch.Count < _maxBatchSize && _requests.Reader.TryRead(out var request))
            {
                batch.Add(request);
            }

            if (batch.Count == _maxBatchSize)
            {
                return;
            }

            var remaining = _maxBatchDelay - Stopwatch.GetElapsedTime(batchStartedAt);
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            using var timeout = new CancellationTokenSource(remaining);
            try
            {
                if (!await _requests.Reader.WaitToReadAsync(timeout.Token))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task PersistBatchAsync(IReadOnlyList<PendingPublish> batch, double assemblyMilliseconds)
    {
        var active = new List<PendingPublish>(batch.Count);
        foreach (var request in batch)
        {
            if (request.CancellationToken.IsCancellationRequested)
            {
                request.Completion.TrySetCanceled(request.CancellationToken);
            }
            else
            {
                active.Add(request);
            }
        }

        if (active.Count == 0)
        {
            return;
        }

        SqliteWriteTiming? writeTiming = null;
        var cleanupMilliseconds = 0d;
        try
        {
            var outcomes = await _database.WriteAsync(async (connection, transaction, token) =>
            {
                var cleanupStarted = Stopwatch.GetTimestamp();
                await SqliteDatabase.ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    "DELETE FROM publications WHERE created_at_utc < $cutoff;",
                    token,
                    ("$cutoff", SqliteDatabase.UtcText(DateTimeOffset.UtcNow.AddHours(-24))));
                cleanupMilliseconds = Stopwatch.GetElapsedTime(cleanupStarted).TotalMilliseconds;

                var results = new List<PublishOutcome>(active.Count);
                foreach (var request in active)
                {
                    try
                    {
                        results.Add(new PublishOutcome(
                            request,
                            await PersistOneAsync(connection, transaction, request, token),
                            Error: null));
                    }
                    catch (KeyNotFoundException exception)
                    {
                        results.Add(new PublishOutcome(request, Result: null, exception));
                    }
                    catch (InvalidOperationException exception) when (exception.Message.Contains("Publish ID", StringComparison.Ordinal))
                    {
                        results.Add(new PublishOutcome(request, Result: null, exception));
                    }
                }

                return results;
            }, CancellationToken.None, timing => writeTiming = timing);

            var timing = writeTiming ?? throw new InvalidOperationException("SQLite batch completed without write timing.");
            foreach (var outcome in outcomes)
            {
                ApplyDiagnostics(outcome.Request, active.Count, assemblyMilliseconds, cleanupMilliseconds, timing);
                if (outcome.Error is not null)
                {
                    outcome.Request.Completion.TrySetException(outcome.Error);
                }
                else
                {
                    outcome.Request.Completion.TrySetResult(outcome.Result!);
                }
            }
        }
        catch (Exception exception)
        {
            foreach (var request in active)
            {
                request.Completion.TrySetException(exception);
            }
        }
    }

    private static async Task<PublishResult> PersistOneAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PendingPublish request,
        CancellationToken ct)
    {
        var stageStarted = Stopwatch.GetTimestamp();
        var fingerprint = Fingerprint(request.Envelope);
        SqliteDatabase.SetElapsed(request.Diagnostics, PublishDiagnosticTags.FingerprintMilliseconds, stageStarted);

        stageStarted = Stopwatch.GetTimestamp();
        var existing = await FindPublicationAsync(connection, transaction, request.PublishId, ct);
        SqliteDatabase.SetElapsed(request.Diagnostics, PublishDiagnosticTags.IdempotencyLookupMilliseconds, stageStarted);
        if (existing is not null)
        {
            if (!StringComparer.Ordinal.Equals(existing.Value.Fingerprint, fingerprint))
            {
                throw new InvalidOperationException("Publish ID was already used with different message data.");
            }

            stageStarted = Stopwatch.GetTimestamp();
            var existingResult = await ReadResultAsync(
                connection,
                transaction,
                request.PublishId,
                existing.Value.MessageId,
                existing.Value.Status,
                ct);
            SqliteDatabase.SetElapsed(request.Diagnostics, PublishDiagnosticTags.ResultReadMilliseconds, stageStarted);
            return existingResult;
        }

        stageStarted = Stopwatch.GetTimestamp();
        var exchange = await FindExchangeAsync(connection, transaction, request.Envelope.ExchangeName, ct)
            ?? throw new KeyNotFoundException($"Exchange '{request.Envelope.ExchangeName}' does not exist.");
        SqliteDatabase.SetElapsed(request.Diagnostics, PublishDiagnosticTags.ExchangeLookupMilliseconds, stageStarted);

        stageStarted = Stopwatch.GetTimestamp();
        var destinations = await ResolveDestinationsAsync(connection, transaction, exchange, request.Envelope.RoutingKey, ct);
        SqliteDatabase.SetElapsed(request.Diagnostics, PublishDiagnosticTags.RoutingMilliseconds, stageStarted);
        var messageId = Guid.NewGuid();
        var status = destinations.Count == 0 ? PublishStatus.Unroutable : PublishStatus.Accepted;

        stageStarted = Stopwatch.GetTimestamp();
        await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, """
            INSERT INTO publications(publish_id, message_id, request_fingerprint, status, created_at_utc)
            VALUES ($publishId, $messageId, $fingerprint, $status, $createdAt);
            """, ct,
            ("$publishId", SqliteDatabase.GuidBytes(request.PublishId)),
            ("$messageId", SqliteDatabase.GuidBytes(messageId)),
            ("$fingerprint", fingerprint),
            ("$status", (int)status),
            ("$createdAt", SqliteDatabase.UtcNowText()));
        SqliteDatabase.SetElapsed(request.Diagnostics, PublishDiagnosticTags.PublicationInsertMilliseconds, stageStarted);

        stageStarted = Stopwatch.GetTimestamp();
        foreach (var queueName in destinations)
        {
            await SqliteMessageQueueStore.InsertMessageAsync(
                connection,
                transaction,
                queueName,
                messageId,
                request.Envelope.Message,
                ct);
            await SqliteDatabase.ExecuteNonQueryAsync(
                connection,
                transaction,
                "INSERT INTO publication_destinations(publish_id, queue_name) VALUES ($publishId, $queue);",
                ct,
                ("$publishId", SqliteDatabase.GuidBytes(request.PublishId)),
                ("$queue", queueName));
        }

        SqliteDatabase.SetElapsed(request.Diagnostics, PublishDiagnosticTags.EnqueueMilliseconds, stageStarted);
        return new PublishResult(request.PublishId, messageId, status, destinations);
    }

    private static void ApplyDiagnostics(
        PendingPublish request,
        int batchSize,
        double assemblyMilliseconds,
        double cleanupMilliseconds,
        SqliteWriteTiming timing)
    {
        if (request.Diagnostics is null)
        {
            return;
        }

        request.Diagnostics.SetTag(PublishDiagnosticTags.BatchSize, batchSize);
        request.Diagnostics.SetTag(PublishDiagnosticTags.BatchAssemblyMilliseconds, assemblyMilliseconds);
        request.Diagnostics.SetTag(
            PublishDiagnosticTags.WriterWaitMilliseconds,
            Stopwatch.GetElapsedTime(request.EnqueuedAt, timing.WriterAcquiredAt).TotalMilliseconds);
        request.Diagnostics.SetTag(PublishDiagnosticTags.ConnectionOpenMilliseconds, timing.ConnectionOpenMilliseconds);
        request.Diagnostics.SetTag(PublishDiagnosticTags.TransactionBeginMilliseconds, timing.TransactionBeginMilliseconds);
        request.Diagnostics.SetTag(PublishDiagnosticTags.TransactionWorkMilliseconds, timing.TransactionWorkMilliseconds);
        request.Diagnostics.SetTag(PublishDiagnosticTags.TransactionCommitMilliseconds, timing.TransactionCommitMilliseconds);
        request.Diagnostics.SetTag(PublishDiagnosticTags.CleanupMilliseconds, cleanupMilliseconds);
    }

    private static async Task<(Guid MessageId, string Fingerprint, PublishStatus Status)?> FindPublicationAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid publishId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT message_id, request_fingerprint, status FROM publications WHERE publish_id=$publishId;";
        command.Parameters.AddWithValue("$publishId", SqliteDatabase.GuidBytes(publishId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? (SqliteDatabase.ReadGuid(reader, 0), reader.GetString(1), (PublishStatus)reader.GetInt32(2))
            : null;
    }

    private static async Task<PublishResult> ReadResultAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid publishId,
        Guid messageId,
        PublishStatus status,
        CancellationToken ct)
    {
        var destinations = new List<string>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT queue_name FROM publication_destinations WHERE publish_id=$publishId ORDER BY queue_name;";
        command.Parameters.AddWithValue("$publishId", SqliteDatabase.GuidBytes(publishId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            destinations.Add(reader.GetString(0));
        }

        return new PublishResult(publishId, messageId, status, destinations);
    }

    private static async Task<Exchange?> FindExchangeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string name,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT name, type, durable FROM exchanges WHERE name=$name;";
        command.Parameters.AddWithValue("$name", name);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new Exchange(reader.GetString(0), (ExchangeType)reader.GetInt32(1), reader.GetInt32(2) != 0)
            : null;
    }

    private static async Task<IReadOnlyList<string>> ResolveDestinationsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Exchange exchange,
        string routingKey,
        CancellationToken ct)
    {
        var bindings = new List<Binding>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT exchange_name, queue_name, routing_key FROM bindings WHERE exchange_name=$exchange;";
        command.Parameters.AddWithValue("$exchange", exchange.Name);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            bindings.Add(new Binding(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return exchange.Type switch
        {
            ExchangeType.Fanout => bindings.Select(x => x.QueueName).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            ExchangeType.Direct => bindings.Where(x => x.RoutingKey == routingKey).Select(x => x.QueueName).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            ExchangeType.Topic => bindings.Where(x => TopicMatcher.Matches(x.RoutingKey, routingKey)).Select(x => x.QueueName).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            _ => throw new InvalidOperationException($"Unknown exchange type: {exchange.Type}")
        };
    }

    private static string Fingerprint(Envelope envelope)
    {
        var metadata = Encoding.UTF8.GetBytes($"{envelope.ExchangeName}\n{envelope.RoutingKey}\n{envelope.Message.CorrelationId:N}\n");
        var data = new byte[metadata.Length + envelope.Message.Payload.Length];
        metadata.CopyTo(data, 0);
        envelope.Message.Payload.Span.CopyTo(data.AsSpan(metadata.Length));
        return Convert.ToHexString(SHA256.HashData(data));
    }

    private sealed record PendingPublish(
        Guid PublishId,
        Envelope Envelope,
        CancellationToken CancellationToken,
        long EnqueuedAt,
        Activity? Diagnostics,
        TaskCompletionSource<PublishResult> Completion);

    private sealed record PublishOutcome(
        PendingPublish Request,
        PublishResult? Result,
        Exception? Error);
}
