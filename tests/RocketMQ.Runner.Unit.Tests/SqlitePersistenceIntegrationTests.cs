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

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
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

    private static Envelope Envelope(string exchange, string routingKey) => new(exchange, routingKey, new InboundMessage(Guid.NewGuid(), Guid.NewGuid(), new byte[] { 1, 2, 3 }, DateTimeOffset.UtcNow));
}





