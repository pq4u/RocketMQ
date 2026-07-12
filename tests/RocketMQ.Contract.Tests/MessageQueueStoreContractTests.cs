using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using Xunit;

namespace RocketMQ.Contract.Tests;

/// <summary>
/// The single source of truth for "is this an IMessageQueueStore
/// implementation." Every adapter — SQLite today, the custom WAL manager
/// tomorrow — inherits this class and only has to implement
/// <see cref="CreateStoreAsync"/>. If both subclasses pass, the two
/// implementations are behaviorally interchangeable, which is the entire
/// point of the port/adapter split.
///
/// Do not weaken these tests to make an adapter pass. If an adapter can't
/// satisfy one of them, that's the adapter's bug, not the test's.
/// </summary>
public abstract class MessageQueueStoreContractTests : IAsyncLifetime
{
    private IMessageQueueStore _store = null!;

    /// <summary>Creates a fresh, empty store instance for one test.</summary>
    protected abstract Task<IMessageQueueStore> CreateStoreAsync();

    /// <summary>Override to clean up whatever CreateStoreAsync allocated (temp files, connections, ...).</summary>
    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    public async ValueTask InitializeAsync() => _store = await CreateStoreAsync();

    public async ValueTask DisposeAsync() => await DisposeStoreAsync();

    private static InboundMessage NewMessage() => new(
        ConnectionId: Guid.NewGuid(),
        CorrelationId: Guid.NewGuid(),
        Payload: new byte[] { 1, 2, 3 },
        ReceivedAtUtc: DateTimeOffset.UtcNow);

    // ── Enqueue & basic Lease ─────────────────────────────────────────

    [Fact]
    public async Task EnqueueAsync_Then_LeaseNextAsync_Returns_The_Message()
    {
        var message = NewMessage();
        await _store.EnqueueAsync(message, CancellationToken.None);

        var leased = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.NotNull(leased);
        Assert.Equal(message.CorrelationId, leased.Message.CorrelationId);
    }

    [Fact]
    public async Task EnqueueAsync_Returns_Unique_MessageIds()
    {
        var id1 = await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        var id2 = await _store.EnqueueAsync(NewMessage(), CancellationToken.None);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task LeaseNextAsync_Returns_Null_When_Queue_Is_Empty()
    {
        var result = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LeaseNextAsync_Returns_Messages_In_FIFO_Order()
    {
        var a = NewMessage();
        var b = NewMessage();
        var c = NewMessage();
        await _store.EnqueueAsync(a, CancellationToken.None);
        await _store.EnqueueAsync(b, CancellationToken.None);
        await _store.EnqueueAsync(c, CancellationToken.None);

        var first = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        var second = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        var third = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Equal(a.CorrelationId, first!.Message.CorrelationId);
        Assert.Equal(b.CorrelationId, second!.Message.CorrelationId);
        Assert.Equal(c.CorrelationId, third!.Message.CorrelationId);
    }

    // ── Lease visibility ──────────────────────────────────────────────

    [Fact]
    public async Task Leased_Message_Is_Not_Visible_To_Other_Consumers()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);

        var first = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        var second = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task Two_Concurrent_LeaseNextAsync_Calls_Never_Return_Same_Message()
    {
        const int count = 20;
        for (var i = 0; i < count; i++)
            await _store.EnqueueAsync(NewMessage(), CancellationToken.None);

        var tasks = Enumerable.Range(0, count)
            .Select(_ => _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var leaseIds = results.Where(r => r != null).Select(r => r!.LeaseId).ToList();

        Assert.Equal(count, leaseIds.Count);
        Assert.Equal(count, leaseIds.Distinct().Count());
    }

    // ── Ack ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AckAsync_Permanently_Removes_Message()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        var leased = await _store.LeaseNextAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        await _store.AckAsync(leased!.LeaseId, CancellationToken.None);

        // Wait for visibility timeout to expire, then verify message is gone
        await Task.Delay(100);
        var result = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task AckAsync_Throws_For_Expired_Lease()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        var leased = await _store.LeaseNextAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        await Task.Delay(100); // Wait for lease to expire

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.AckAsync(leased!.LeaseId, CancellationToken.None));
    }

    [Fact]
    public async Task AckAsync_Throws_For_Unknown_LeaseId()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.AckAsync(Guid.NewGuid(), CancellationToken.None));
    }

    // ── Nack with requeue=true ────────────────────────────────────────

    [Fact]
    public async Task NackAsync_Requeue_True_Makes_Message_Available_Again()
    {
        var message = NewMessage();
        await _store.EnqueueAsync(message, CancellationToken.None);
        var leased = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        await _store.NackAsync(leased!.LeaseId, requeue: true, CancellationToken.None);

        var again = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(again);
        Assert.Equal(message.CorrelationId, again.Message.CorrelationId);
    }

    [Fact]
    public async Task NackAsync_Requeue_True_Preserves_DeliveryCount()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        var leased = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(1, leased!.DeliveryCount);

        await _store.NackAsync(leased.LeaseId, requeue: true, CancellationToken.None);

        var again = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(2, again!.DeliveryCount);
    }

    // ── Nack with requeue=false (dead-letter) ─────────────────────────

    [Fact]
    public async Task NackAsync_Requeue_False_Dead_Letters_Message()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        var leased = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        await _store.NackAsync(leased!.LeaseId, requeue: false, CancellationToken.None);

        var result = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task NackAsync_Requeue_False_Message_Appears_In_DeadLetters()
    {
        var message = NewMessage();
        await _store.EnqueueAsync(message, CancellationToken.None);
        var leased = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        await _store.NackAsync(leased!.LeaseId, requeue: false, CancellationToken.None);

        var deadLetters = await CollectAsync(_store.BrowseDeadLettersAsync(CancellationToken.None));
        Assert.Single(deadLetters);
        Assert.Equal(message.CorrelationId, deadLetters[0].Message.CorrelationId);
        Assert.Equal(1, deadLetters[0].DeliveryCount);
        Assert.True(deadLetters[0].DeadLetteredAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task NackAsync_Throws_For_Expired_Lease()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        var leased = await _store.LeaseNextAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        await Task.Delay(100); // Wait for lease to expire

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.NackAsync(leased!.LeaseId, requeue: true, CancellationToken.None));
    }

    // ── Visibility timeout & auto-redelivery ──────────────────────────

    [Fact]
    public async Task Expired_Lease_Makes_Message_Available_For_Redelivery()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        await _store.LeaseNextAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        await Task.Delay(100); // Wait for visibility timeout to expire

        var redelivered = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(redelivered);
    }

    [Fact]
    public async Task Redelivery_Increments_DeliveryCount()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        var first = await _store.LeaseNextAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.Equal(1, first!.DeliveryCount);

        await Task.Delay(100); // Wait for visibility timeout to expire

        var second = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(2, second!.DeliveryCount);
    }

    [Fact]
    public async Task Active_Lease_Prevents_Redelivery()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);
        await _store.LeaseNextAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Immediately try again — lease is still active
        var result = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Null(result);
    }

    // ── Concurrency & durability ──────────────────────────────────────

    [Fact]
    public async Task Concurrent_Enqueues_Do_Not_Lose_Messages()
    {
        const int concurrency = 20;
        var messages = Enumerable.Range(0, concurrency).Select(_ => NewMessage()).ToList();

        await Task.WhenAll(messages.Select(m => _store.EnqueueAsync(m, CancellationToken.None)));

        var leased = new List<LeasedMessage>();
        while (true)
        {
            var next = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
            if (next == null) break;
            leased.Add(next);
        }

        var leasedCorrelationIds = leased.Select(l => l.Message.CorrelationId).ToHashSet();
        foreach (var message in messages)
        {
            Assert.Contains(message.CorrelationId, leasedCorrelationIds);
        }
    }

    [Fact]
    public async Task DeliveryCount_Starts_At_One_On_First_Lease()
    {
        await _store.EnqueueAsync(NewMessage(), CancellationToken.None);

        var leased = await _store.LeaseNextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Equal(1, leased!.DeliveryCount);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}
