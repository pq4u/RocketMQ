using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using Xunit;

namespace RocketMQ.Contract.Tests;

/// <summary>
/// The single source of truth for "is this an IRoutingStore implementation."
/// Every adapter inherits this class and only has to implement
/// <see cref="CreateStoreAsync"/>. If both subclasses pass, the two
/// implementations are behaviorally interchangeable.
///
/// Do not weaken these tests to make an adapter pass. If an adapter can't
/// satisfy one of them, that's the adapter's bug, not the test's.
/// </summary>
public abstract class RoutingStoreContractTests : IAsyncLifetime
{
    private IRoutingStore _store = null!;

    /// <summary>Creates a fresh, empty store instance for one test.</summary>
    protected abstract Task<IRoutingStore> CreateStoreAsync();

    /// <summary>Override to clean up whatever CreateStoreAsync allocated (temp files, connections, ...).</summary>
    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    public async ValueTask InitializeAsync() => _store = await CreateStoreAsync();

    public async ValueTask DisposeAsync() => await DisposeStoreAsync();

    // ── Exchange tests ────────────────────────────────────────────────

    [Fact]
    public async Task DeclareExchangeAsync_Then_GetExchangeAsync_Returns_Exchange()
    {
        var exchange = new Exchange("test-exchange", ExchangeType.Direct, Durable: true);

        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);

        var result = await _store.GetExchangeAsync("test-exchange", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(exchange.Name, result.Name);
        Assert.Equal(exchange.Type, result.Type);
        Assert.Equal(exchange.Durable, result.Durable);
    }

    [Fact]
    public async Task DeclareExchangeAsync_Is_Idempotent_With_Same_Config()
    {
        var exchange = new Exchange("idempotent-exchange", ExchangeType.Fanout, Durable: true);

        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);

        var result = await _store.GetExchangeAsync("idempotent-exchange", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(exchange.Name, result.Name);
    }

    [Fact]
    public async Task DeclareExchangeAsync_Throws_On_Different_Config()
    {
        var original = new Exchange("conflict-exchange", ExchangeType.Direct, Durable: true);
        var conflicting = new Exchange("conflict-exchange", ExchangeType.Fanout, Durable: true);

        await _store.DeclareExchangeAsync(original, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.DeclareExchangeAsync(conflicting, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteExchangeAsync_Removes_Exchange()
    {
        var exchange = new Exchange("delete-me", ExchangeType.Direct, Durable: true);
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);

        await _store.DeleteExchangeAsync("delete-me", CancellationToken.None);

        var result = await _store.GetExchangeAsync("delete-me", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteExchangeAsync_Is_NoOp_For_NonExistent()
    {
        // Should not throw
        await _store.DeleteExchangeAsync("does-not-exist", CancellationToken.None);
    }

    [Fact]
    public async Task DeleteExchangeAsync_Also_Removes_Bindings()
    {
        var exchange = new Exchange("bound-exchange", ExchangeType.Direct, Durable: true);
        var queue = new QueueDefinition("bound-queue", Durable: true, MaxDeliveryCount: 5);
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);
        await _store.DeclareQueueAsync(queue, CancellationToken.None);
        await _store.BindAsync(new Binding("bound-exchange", "bound-queue", "key"), CancellationToken.None);

        await _store.DeleteExchangeAsync("bound-exchange", CancellationToken.None);

        // Re-declare exchange so we can query bindings
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);
        var bindings = await _store.GetBindingsAsync("bound-exchange", CancellationToken.None);
        Assert.Empty(bindings);
    }

    [Fact]
    public async Task ListExchangesAsync_Returns_All_Declared()
    {
        var e1 = new Exchange("ex-1", ExchangeType.Direct, Durable: true);
        var e2 = new Exchange("ex-2", ExchangeType.Fanout, Durable: false);
        await _store.DeclareExchangeAsync(e1, CancellationToken.None);
        await _store.DeclareExchangeAsync(e2, CancellationToken.None);

        var all = await _store.ListExchangesAsync(CancellationToken.None);

        Assert.Contains(all, e => e.Name == "ex-1");
        Assert.Contains(all, e => e.Name == "ex-2");
        Assert.True(all.Count >= 2);
    }

    // ── Queue tests ───────────────────────────────────────────────────

    [Fact]
    public async Task DeclareQueueAsync_Then_GetQueueAsync_Returns_Queue()
    {
        var queue = new QueueDefinition("test-queue", Durable: true, MaxDeliveryCount: 5);

        await _store.DeclareQueueAsync(queue, CancellationToken.None);

        var result = await _store.GetQueueAsync("test-queue", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(queue.Name, result.Name);
        Assert.Equal(queue.Durable, result.Durable);
        Assert.Equal(queue.MaxDeliveryCount, result.MaxDeliveryCount);
    }

    [Fact]
    public async Task DeclareQueueAsync_Is_Idempotent_With_Same_Config()
    {
        var queue = new QueueDefinition("idempotent-queue", Durable: true, MaxDeliveryCount: 3);

        await _store.DeclareQueueAsync(queue, CancellationToken.None);
        await _store.DeclareQueueAsync(queue, CancellationToken.None);

        var result = await _store.GetQueueAsync("idempotent-queue", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(queue.Name, result.Name);
    }

    [Fact]
    public async Task DeclareQueueAsync_Throws_On_Different_Config()
    {
        var original = new QueueDefinition("conflict-queue", Durable: true, MaxDeliveryCount: 5);
        var conflicting = new QueueDefinition("conflict-queue", Durable: false, MaxDeliveryCount: 5);

        await _store.DeclareQueueAsync(original, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.DeclareQueueAsync(conflicting, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteQueueAsync_Removes_Queue()
    {
        var queue = new QueueDefinition("delete-me", Durable: true, MaxDeliveryCount: 0);
        await _store.DeclareQueueAsync(queue, CancellationToken.None);

        await _store.DeleteQueueAsync("delete-me", CancellationToken.None);

        var result = await _store.GetQueueAsync("delete-me", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteQueueAsync_Is_NoOp_For_NonExistent()
    {
        // Should not throw
        await _store.DeleteQueueAsync("does-not-exist", CancellationToken.None);
    }

    [Fact]
    public async Task DeleteQueueAsync_Also_Removes_Bindings()
    {
        var exchange = new Exchange("ex-for-queue-delete", ExchangeType.Direct, Durable: true);
        var queue = new QueueDefinition("queue-to-delete", Durable: true, MaxDeliveryCount: 5);
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);
        await _store.DeclareQueueAsync(queue, CancellationToken.None);
        await _store.BindAsync(new Binding("ex-for-queue-delete", "queue-to-delete", "key"), CancellationToken.None);

        await _store.DeleteQueueAsync("queue-to-delete", CancellationToken.None);

        var bindings = await _store.GetBindingsAsync("ex-for-queue-delete", CancellationToken.None);
        Assert.Empty(bindings);
    }

    [Fact]
    public async Task ListQueuesAsync_Returns_All_Declared()
    {
        var q1 = new QueueDefinition("q-1", Durable: true, MaxDeliveryCount: 0);
        var q2 = new QueueDefinition("q-2", Durable: false, MaxDeliveryCount: 10);
        await _store.DeclareQueueAsync(q1, CancellationToken.None);
        await _store.DeclareQueueAsync(q2, CancellationToken.None);

        var all = await _store.ListQueuesAsync(CancellationToken.None);

        Assert.Contains(all, q => q.Name == "q-1");
        Assert.Contains(all, q => q.Name == "q-2");
        Assert.True(all.Count >= 2);
    }

    // ── Binding tests ─────────────────────────────────────────────────

    [Fact]
    public async Task BindAsync_Then_GetBindingsAsync_Returns_Binding()
    {
        var exchange = new Exchange("bind-exchange", ExchangeType.Direct, Durable: true);
        var queue = new QueueDefinition("bind-queue", Durable: true, MaxDeliveryCount: 5);
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);
        await _store.DeclareQueueAsync(queue, CancellationToken.None);

        var binding = new Binding("bind-exchange", "bind-queue", "my.key");
        await _store.BindAsync(binding, CancellationToken.None);

        var bindings = await _store.GetBindingsAsync("bind-exchange", CancellationToken.None);
        var single = Assert.Single(bindings);
        Assert.Equal("bind-exchange", single.ExchangeName);
        Assert.Equal("bind-queue", single.QueueName);
        Assert.Equal("my.key", single.RoutingKey);
    }

    [Fact]
    public async Task BindAsync_Throws_For_NonExistent_Exchange()
    {
        var queue = new QueueDefinition("orphan-queue", Durable: true, MaxDeliveryCount: 0);
        await _store.DeclareQueueAsync(queue, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.BindAsync(
                new Binding("no-such-exchange", "orphan-queue", "key"),
                CancellationToken.None));
    }

    [Fact]
    public async Task BindAsync_Throws_For_NonExistent_Queue()
    {
        var exchange = new Exchange("lonely-exchange", ExchangeType.Direct, Durable: true);
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.BindAsync(
                new Binding("lonely-exchange", "no-such-queue", "key"),
                CancellationToken.None));
    }

    [Fact]
    public async Task UnbindAsync_Removes_Binding()
    {
        var exchange = new Exchange("unbind-exchange", ExchangeType.Direct, Durable: true);
        var queue = new QueueDefinition("unbind-queue", Durable: true, MaxDeliveryCount: 0);
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);
        await _store.DeclareQueueAsync(queue, CancellationToken.None);
        await _store.BindAsync(new Binding("unbind-exchange", "unbind-queue", "key"), CancellationToken.None);

        await _store.UnbindAsync("unbind-exchange", "unbind-queue", "key", CancellationToken.None);

        var bindings = await _store.GetBindingsAsync("unbind-exchange", CancellationToken.None);
        Assert.Empty(bindings);
    }

    [Fact]
    public async Task UnbindAsync_Is_NoOp_For_NonExistent()
    {
        // Should not throw
        await _store.UnbindAsync("no-exchange", "no-queue", "no-key", CancellationToken.None);
    }

    [Fact]
    public async Task GetBindingsAsync_Returns_Empty_For_No_Bindings()
    {
        var exchange = new Exchange("empty-exchange", ExchangeType.Fanout, Durable: true);
        await _store.DeclareExchangeAsync(exchange, CancellationToken.None);

        var bindings = await _store.GetBindingsAsync("empty-exchange", CancellationToken.None);

        Assert.Empty(bindings);
    }
}
