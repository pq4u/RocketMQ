using System.Diagnostics;
using Microsoft.Data.Sqlite;
using RocketMQ.Core.Diagnostics;
using RocketMQ.Core.Models;
using RocketMQ.Persistence.Sqlite;

namespace RocketMQ.Runner.Unit.Tests;

public sealed class SqlitePersistenceIntegrationTests : IAsyncLifetime
{
    private string _databasePath = null!;
    private SqliteDatabase _database = null!;
    private SqliteRoutingStore _routing = null!;
    private SqliteMessageQueueStore _queues = null!;
    private SqliteMessagePublisher _publisher = null!;

    public ValueTask InitializeAsync()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"rocketmq-{Guid.NewGuid():N}.db");
        _database = new SqliteDatabase($"Data Source={_databasePath};Mode=ReadWriteCreate;Cache=Shared;Pooling=False");
        _routing = new SqliteRoutingStore(_database);
        _queues = new SqliteMessageQueueStore(_database);
        _publisher = new SqliteMessagePublisher(_database);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _publisher.DisposeAsync();
        SqliteConnection.ClearAllPools();
        DeleteIfExists(_databasePath);
        DeleteIfExists(_databasePath + "-wal");
        DeleteIfExists(_databasePath + "-shm");
    }

    [Fact]
    public async Task Publish_Fanout_IsAtomic_AndRetryUsesOriginalOutcome()
    {
        await _routing.DeclareExchangeAsync(new Exchange("events", ExchangeType.Fanout, true), CancellationToken.None);
        await _routing.DeclareQueueAsync(new QueueDefinition("a", true, 3), CancellationToken.None);
        await _routing.DeclareQueueAsync(new QueueDefinition("b", true, 3), CancellationToken.None);
        await _routing.BindAsync(new Binding("events", "a", ""), CancellationToken.None);
        await _routing.BindAsync(new Binding("events", "b", ""), CancellationToken.None);
        var publishId = Guid.NewGuid();
        var envelope = Envelope("events", "any");

        var first = await _publisher.PublishAsync(publishId, envelope, CancellationToken.None);
        var retryEnvelope = new Envelope("events", "any", new InboundMessage(Guid.NewGuid(), envelope.Message.CorrelationId, envelope.Message.Payload, DateTimeOffset.UtcNow));
        var retry = await _publisher.PublishAsync(publishId, retryEnvelope, CancellationToken.None);

        Assert.Equal(PublishStatus.Accepted, first.Status);
        Assert.Equal(["a", "b"], first.DestinationQueues);
        Assert.Equal(first.PublishId, retry.PublishId);
        Assert.Equal(first.MessageId, retry.MessageId);
        Assert.Equal(first.Status, retry.Status);
        Assert.Equal(first.DestinationQueues, retry.DestinationQueues);
        var a = await _queues.LeaseNextAsync("a", TimeSpan.FromSeconds(30), CancellationToken.None);
        var b = await _queues.LeaseNextAsync("b", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(first.MessageId, a.MessageId);
        Assert.Equal(first.MessageId, b.MessageId);
        Assert.Null(await _queues.LeaseNextAsync("a", TimeSpan.FromSeconds(30), CancellationToken.None));
        Assert.Null(await _queues.LeaseNextAsync("b", TimeSpan.FromSeconds(30), CancellationToken.None));
    }

    [Fact]
    public async Task Queue_RejectsUndeclaredQueue_AndDeadLettersAfterMaximumDeliveries()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _queues.EnqueueAsync("missing", Envelope("none", "").Message, CancellationToken.None));
        await _routing.DeclareQueueAsync(new QueueDefinition("limited", true, 1), CancellationToken.None);
        await _queues.EnqueueAsync("limited", Envelope("none", "").Message, CancellationToken.None);
        var lease = await _queues.LeaseNextAsync("limited", TimeSpan.FromMilliseconds(1), CancellationToken.None);
        await Task.Delay(25, TestContext.Current.CancellationToken);
        Assert.Null(await _queues.LeaseNextAsync("limited", TimeSpan.FromSeconds(1), CancellationToken.None));
        var deadLetters = new List<DeadLetteredMessage>();
        await foreach (var row in _queues.BrowseDeadLettersAsync("limited", CancellationToken.None)) deadLetters.Add(row);
        Assert.Equal("max-delivery-count-exceeded", Assert.Single(deadLetters).Reason);
        Assert.NotNull(lease);
    }

    [Fact]
    public async Task Publish_DiagnosticsEnabled_RecordsWriterAndSqlStages()
    {
        await _routing.DeclareExchangeAsync(new Exchange("diagnostics", ExchangeType.Direct, true), CancellationToken.None);
        await _routing.DeclareQueueAsync(new QueueDefinition("diagnostics", true, 3), CancellationToken.None);
        await _routing.BindAsync(new Binding("diagnostics", "diagnostics", "key"), CancellationToken.None);
        using var activity = new Activity("publish-diagnostics").Start();
        activity.SetTag(PublishDiagnosticTags.Enabled, true);

        await _publisher.PublishAsync(Guid.NewGuid(), Envelope("diagnostics", "key"), CancellationToken.None);

        Assert.IsType<double>(activity.GetTagItem(PublishDiagnosticTags.WriterWaitMilliseconds));
        Assert.IsType<double>(activity.GetTagItem(PublishDiagnosticTags.ConnectionOpenMilliseconds));
        Assert.IsType<double>(activity.GetTagItem(PublishDiagnosticTags.TransactionWorkMilliseconds));
        Assert.IsType<double>(activity.GetTagItem(PublishDiagnosticTags.TransactionCommitMilliseconds));
        Assert.IsType<double>(activity.GetTagItem(PublishDiagnosticTags.CleanupMilliseconds));
        Assert.IsType<double>(activity.GetTagItem(PublishDiagnosticTags.EnqueueMilliseconds));
        Assert.Equal(1, Assert.IsType<int>(activity.GetTagItem(PublishDiagnosticTags.BatchSize)));
        Assert.IsType<double>(activity.GetTagItem(PublishDiagnosticTags.BatchAssemblyMilliseconds));
    }

    [Fact]
    public async Task Publish_CommitsImmediatelyWhenBatchReachesMaximumSize()
    {
        await _routing.DeclareExchangeAsync(new Exchange("full-batch", ExchangeType.Direct, true), CancellationToken.None);
        await _routing.DeclareQueueAsync(new QueueDefinition("full-batch", true, 3), CancellationToken.None);
        await _routing.BindAsync(new Binding("full-batch", "full-batch", "key"), CancellationToken.None);
        await using var publisher = new SqliteMessagePublisher(
            _database,
            maxBatchSize: 4,
            maxBatchDelay: TimeSpan.FromSeconds(30),
            queueCapacity: 4);

        var publishes = Enumerable.Range(0, 4)
            .Select(_ => PublishWithDiagnosticsAsync(publisher, Envelope("full-batch", "key")))
            .ToArray();
        var completed = await Task.WhenAll(publishes).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(completed, item => Assert.Equal(4, Assert.IsType<int>(
            item.Diagnostics.GetTagItem(PublishDiagnosticTags.BatchSize))));
    }

    [Fact]
    public async Task Publish_CommitsPartialBatchAfterMaximumDelay()
    {
        await _routing.DeclareExchangeAsync(new Exchange("partial-batch", ExchangeType.Direct, true), CancellationToken.None);
        await _routing.DeclareQueueAsync(new QueueDefinition("partial-batch", true, 3), CancellationToken.None);
        await _routing.BindAsync(new Binding("partial-batch", "partial-batch", "key"), CancellationToken.None);
        await using var publisher = new SqliteMessagePublisher(
            _database,
            maxBatchSize: 4,
            maxBatchDelay: TimeSpan.FromMilliseconds(20),
            queueCapacity: 4);

        var completed = await PublishWithDiagnosticsAsync(publisher, Envelope("partial-batch", "key"))
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(PublishStatus.Accepted, completed.Result.Status);
        Assert.Equal(1, Assert.IsType<int>(completed.Diagnostics.GetTagItem(PublishDiagnosticTags.BatchSize)));
    }

    [Fact]
    public async Task Initialization_UpgradesVersion1DatabaseWithPublicationRetentionIndex()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"rocketmq-v1-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Pooling=False";
        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE schema_migrations (
                        version INTEGER PRIMARY KEY,
                        applied_at_utc TEXT NOT NULL
                    );
                    CREATE TABLE publications (
                        publish_id BLOB PRIMARY KEY,
                        message_id BLOB NOT NULL,
                        request_fingerprint TEXT NOT NULL,
                        status INTEGER NOT NULL,
                        created_at_utc TEXT NOT NULL
                    );
                    INSERT INTO schema_migrations(version, applied_at_utc)
                    VALUES (1, '2026-01-01T00:00:00.0000000+00:00');
                    """;
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            var routing = new SqliteRoutingStore(new SqliteDatabase(connectionString));
            await routing.GetExchangeAsync("missing", TestContext.Current.CancellationToken);

            await using var verificationConnection = new SqliteConnection(connectionString);
            await verificationConnection.OpenAsync(TestContext.Current.CancellationToken);
            await using var verification = verificationConnection.CreateCommand();
            verification.CommandText = """
                SELECT
                    EXISTS(SELECT 1 FROM schema_migrations WHERE version=2),
                    EXISTS(SELECT 1 FROM sqlite_master
                           WHERE type='index' AND name='ix_publications_created_at'
                             AND tbl_name='publications');
                """;
            await using var reader = await verification.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal(1, reader.GetInt32(1));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");
        }
    }

    private static Envelope Envelope(string exchange, string routingKey) => new(exchange, routingKey, new InboundMessage(Guid.NewGuid(), Guid.NewGuid(), new byte[] { 1, 2, 3 }, DateTimeOffset.UtcNow));

    private static async Task<(PublishResult Result, Activity Diagnostics)> PublishWithDiagnosticsAsync(
        SqliteMessagePublisher publisher,
        Envelope envelope)
    {
        var activity = new Activity("publish-batch-diagnostics").Start();
        activity.SetTag(PublishDiagnosticTags.Enabled, true);
        try
        {
            var result = await publisher.PublishAsync(Guid.NewGuid(), envelope, CancellationToken.None);
            return (result, activity);
        }
        finally
        {
            activity.Stop();
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}





