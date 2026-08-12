using RocketMQ.Core.Models;
using RocketMQ.Runner;

namespace RocketMQ.Runner.Unit.Tests;

public sealed class DeliverySemanticsTests
{
    [Fact]
    public async Task ExpiredLease_IsDeadLetteredAfterMaximumDeliveryCount()
    {
        var clock = new ManualTimeProvider();
        var routingStore = new InMemoryRoutingStore();
        await routingStore.DeclareQueueAsync(
            new QueueDefinition("limited", Durable: true, MaxDeliveryCount: 1),
            CancellationToken.None);

        var queueStore = new InMemoryMessageQueueStore(routingStore, clock);
        var message = NewMessage();

        await queueStore.EnqueueAsync("limited", message, CancellationToken.None);
        var first = await queueStore.LeaseNextAsync(
            "limited",
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(1));

        var next = await queueStore.LeaseNextAsync(
            "limited",
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var deadLetters = await CollectAsync(
            queueStore.BrowseDeadLettersAsync("limited", CancellationToken.None));

        Assert.NotNull(first);
        Assert.Null(next);
        var deadLetter = Assert.Single(deadLetters);
        Assert.Equal(first.MessageId, deadLetter.MessageId);
        Assert.Equal(1, deadLetter.DeliveryCount);
        Assert.Equal("max-delivery-count-exceeded", deadLetter.Reason);
    }

    [Fact]
    public async Task Redelivery_PreservesMessageId_AndCreatesNewLeaseId()
    {
        var clock = new ManualTimeProvider();
        var queueStore = new InMemoryMessageQueueStore(timeProvider: clock);
        await queueStore.EnqueueAsync("queue", NewMessage(), CancellationToken.None);

        var first = await queueStore.LeaseNextAsync(
            "queue",
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(1));

        var second = await queueStore.LeaseNextAsync(
            "queue",
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.MessageId, second.MessageId);
        Assert.NotEqual(first.LeaseId, second.LeaseId);
        Assert.Equal(2, second.DeliveryCount);
    }

    [Fact]
    public async Task AckAtLeaseDeadline_IsRejectedAsExpired()
    {
        var clock = new ManualTimeProvider();
        var queueStore = new InMemoryMessageQueueStore(timeProvider: clock);
        await queueStore.EnqueueAsync("queue", NewMessage(), CancellationToken.None);

        var lease = await queueStore.LeaseNextAsync(
            "queue",
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queueStore.AckAsync(lease!.LeaseId, CancellationToken.None));
    }

    [Fact]
    public async Task LeaseNext_RejectsNonPositiveVisibilityTimeout()
    {
        var queueStore = new InMemoryMessageQueueStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => queueStore.LeaseNextAsync(
                "queue",
                TimeSpan.Zero,
                CancellationToken.None));
    }

    private static InboundMessage NewMessage()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new byte[] { 1, 2, 3 },
            DateTimeOffset.UtcNow);

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
            => _utcNow;

        public void Advance(TimeSpan amount)
            => _utcNow += amount;
    }
}
