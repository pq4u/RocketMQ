using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Core.Routing;

namespace RocketMQ.Runner.Unit.Tests;

public class MessageRouterTests
{
    private class InMemoryRoutingStore : IRoutingStore
    {
        private readonly Dictionary<string, Exchange> _exchanges = new();
        private readonly Dictionary<string, QueueDefinition> _queues = new();
        private readonly List<Binding> _bindings = new();

        public Task DeclareExchangeAsync(Exchange exchange, CancellationToken ct)
        {
            if (_exchanges.TryGetValue(exchange.Name, out var existing))
            {
                if (existing.Type != exchange.Type || existing.Durable != exchange.Durable)
                    throw new InvalidOperationException("Exchange exists with different config.");
                return Task.CompletedTask;
            }
            _exchanges[exchange.Name] = exchange;
            return Task.CompletedTask;
        }

        public Task DeleteExchangeAsync(string exchangeName, CancellationToken ct)
        {
            _exchanges.Remove(exchangeName);
            _bindings.RemoveAll(b => b.ExchangeName == exchangeName);
            return Task.CompletedTask;
        }

        public Task<Exchange?> GetExchangeAsync(string exchangeName, CancellationToken ct)
        {
            _exchanges.TryGetValue(exchangeName, out var exchange);
            return Task.FromResult(exchange);
        }

        public Task<IReadOnlyList<Exchange>> ListExchangesAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Exchange>>(_exchanges.Values.ToList());

        public Task DeclareQueueAsync(QueueDefinition queue, CancellationToken ct)
        {
            if (_queues.TryGetValue(queue.Name, out var existing))
            {
                if (existing.Durable != queue.Durable || existing.MaxDeliveryCount != queue.MaxDeliveryCount)
                    throw new InvalidOperationException("Queue exists with different config.");
                return Task.CompletedTask;
            }
            _queues[queue.Name] = queue;
            return Task.CompletedTask;
        }

        public Task DeleteQueueAsync(string queueName, CancellationToken ct)
        {
            _queues.Remove(queueName);
            _bindings.RemoveAll(b => b.QueueName == queueName);
            return Task.CompletedTask;
        }

        public Task<QueueDefinition?> GetQueueAsync(string queueName, CancellationToken ct)
        {
            _queues.TryGetValue(queueName, out var queue);
            return Task.FromResult(queue);
        }

        public Task<IReadOnlyList<QueueDefinition>> ListQueuesAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<QueueDefinition>>(_queues.Values.ToList());

        public Task BindAsync(Binding binding, CancellationToken ct)
        {
            if (!_exchanges.ContainsKey(binding.ExchangeName))
                throw new InvalidOperationException("Exchange does not exist");
            if (!_queues.ContainsKey(binding.QueueName))
                throw new InvalidOperationException("Queue does not exist");

            if (!_bindings.Any(b => b.ExchangeName == binding.ExchangeName && 
                                    b.QueueName == binding.QueueName && 
                                    b.RoutingKey == binding.RoutingKey))
            {
                _bindings.Add(binding);
            }
            return Task.CompletedTask;
        }

        public Task UnbindAsync(string exchangeName, string queueName, string routingKey, CancellationToken ct)
        {
            _bindings.RemoveAll(b => b.ExchangeName == exchangeName && 
                                     b.QueueName == queueName && 
                                     b.RoutingKey == routingKey);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Binding>> GetBindingsAsync(string exchangeName, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Binding>>(_bindings.Where(b => b.ExchangeName == exchangeName).ToList());
    }

    [Fact]
    public async Task Direct_Exchange_Routes_To_Exact_Match()
    {
        var store = new InMemoryRoutingStore();
        await store.DeclareExchangeAsync(new Exchange("ex", ExchangeType.Direct, true), CancellationToken.None);
        await store.DeclareQueueAsync(new QueueDefinition("q1", true, 0), CancellationToken.None);
        await store.BindAsync(new Binding("ex", "q1", "key1"), CancellationToken.None);

        var router = new MessageRouter(store);
        var queues = await router.ResolveAsync("ex", "key1", CancellationToken.None);

        Assert.Single(queues);
        Assert.Equal("q1", queues[0]);
    }

    [Fact]
    public async Task Direct_Exchange_No_Match_Returns_Empty()
    {
        var store = new InMemoryRoutingStore();
        await store.DeclareExchangeAsync(new Exchange("ex", ExchangeType.Direct, true), CancellationToken.None);
        await store.DeclareQueueAsync(new QueueDefinition("q1", true, 0), CancellationToken.None);
        await store.BindAsync(new Binding("ex", "q1", "key1"), CancellationToken.None);

        var router = new MessageRouter(store);
        var queues = await router.ResolveAsync("ex", "key2", CancellationToken.None);

        Assert.Empty(queues);
    }

    [Fact]
    public async Task Fanout_Exchange_Routes_To_All_Bound_Queues()
    {
        var store = new InMemoryRoutingStore();
        await store.DeclareExchangeAsync(new Exchange("ex", ExchangeType.Fanout, true), CancellationToken.None);
        await store.DeclareQueueAsync(new QueueDefinition("q1", true, 0), CancellationToken.None);
        await store.DeclareQueueAsync(new QueueDefinition("q2", true, 0), CancellationToken.None);
        await store.BindAsync(new Binding("ex", "q1", "ignored"), CancellationToken.None);
        await store.BindAsync(new Binding("ex", "q2", "ignored2"), CancellationToken.None);

        var router = new MessageRouter(store);
        var queues = await router.ResolveAsync("ex", "anything", CancellationToken.None);

        Assert.Equal(2, queues.Count);
        Assert.Contains("q1", queues);
        Assert.Contains("q2", queues);
    }

    [Fact]
    public async Task Topic_Exchange_Star_Matches_One_Word()
    {
        var store = new InMemoryRoutingStore();
        await store.DeclareExchangeAsync(new Exchange("ex", ExchangeType.Topic, true), CancellationToken.None);
        await store.DeclareQueueAsync(new QueueDefinition("q1", true, 0), CancellationToken.None);
        await store.BindAsync(new Binding("ex", "q1", "user.*.created"), CancellationToken.None);

        var router = new MessageRouter(store);
        var queues = await router.ResolveAsync("ex", "user.123.created", CancellationToken.None);
        Assert.Single(queues);

        queues = await router.ResolveAsync("ex", "user.123.deleted", CancellationToken.None);
        Assert.Empty(queues);

        queues = await router.ResolveAsync("ex", "user.123.456.created", CancellationToken.None);
        Assert.Empty(queues);
    }

    [Fact]
    public async Task Topic_Exchange_Hash_Matches_Zero_Or_More()
    {
        var store = new InMemoryRoutingStore();
        await store.DeclareExchangeAsync(new Exchange("ex", ExchangeType.Topic, true), CancellationToken.None);
        await store.DeclareQueueAsync(new QueueDefinition("q1", true, 0), CancellationToken.None);
        await store.BindAsync(new Binding("ex", "q1", "user.#"), CancellationToken.None);

        var router = new MessageRouter(store);
        var queues = await router.ResolveAsync("ex", "user.123", CancellationToken.None);
        Assert.Single(queues);

        queues = await router.ResolveAsync("ex", "user.123.created", CancellationToken.None);
        Assert.Single(queues);

        queues = await router.ResolveAsync("ex", "user", CancellationToken.None);
        Assert.Single(queues);
    }

    [Fact]
    public async Task ResolveAsync_Throws_For_NonExistent_Exchange()
    {
        var store = new InMemoryRoutingStore();
        var router = new MessageRouter(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.ResolveAsync("nonexistent", "key", CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_Queue_Names_Are_Deduplicated()
    {
        var store = new InMemoryRoutingStore();
        await store.DeclareExchangeAsync(new Exchange("ex", ExchangeType.Topic, true), CancellationToken.None);
        await store.DeclareQueueAsync(new QueueDefinition("q1", true, 0), CancellationToken.None);
        
        // Bind the same queue with two different keys that both match the published key
        await store.BindAsync(new Binding("ex", "q1", "#"), CancellationToken.None);
        await store.BindAsync(new Binding("ex", "q1", "user.*"), CancellationToken.None);

        var router = new MessageRouter(store);
        var queues = await router.ResolveAsync("ex", "user.created", CancellationToken.None);

        // Even though both bindings matched, the queue should only be returned once
        Assert.Single(queues);
        Assert.Equal("q1", queues[0]);
    }
}
