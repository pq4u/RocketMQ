using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Runner;

public class InMemoryMessageQueueStore : IMessageQueueStore
{
    private class MessageEntry
    {
        public Guid Id { get; set; }
        public InboundMessage Message { get; set; } = null!;
        public string State { get; set; } = "available"; // available, leased, dead_lettered
        public Guid? LeaseId { get; set; }
        public DateTime? LeaseExpiresAt { get; set; }
        public int DeliveryCount { get; set; }
        public DateTime EnqueuedAt { get; set; }
    }

    private readonly ConcurrentDictionary<string, List<MessageEntry>> _queues = new();
    private readonly object _lock = new();

    public Task<Guid> EnqueueAsync(string queueName, InboundMessage message, CancellationToken ct)
    {
        lock (_lock)
        {
            var id = Guid.NewGuid();
            var entry = new MessageEntry
            {
                Id = id,
                Message = message,
                EnqueuedAt = DateTime.UtcNow
            };
            if (!_queues.ContainsKey(queueName)) _queues[queueName] = new List<MessageEntry>();
            _queues[queueName].Add(entry);
            return Task.FromResult(id);
        }
    }

    public Task<LeasedMessage?> LeaseNextAsync(string queueName, TimeSpan visibilityTimeout, CancellationToken ct)
    {
        lock (_lock)
        {
            if (!_queues.ContainsKey(queueName)) return Task.FromResult<LeasedMessage?>(null);

            var now = DateTime.UtcNow;
            var entry = _queues[queueName]
                .Where(e => e.State == "available" || (e.State == "leased" && e.LeaseExpiresAt < now))
                .OrderBy(e => e.EnqueuedAt)
                .FirstOrDefault();

            if (entry == null) return Task.FromResult<LeasedMessage?>(null);

            entry.State = "leased";
            entry.LeaseId = Guid.NewGuid();
            entry.LeaseExpiresAt = now + visibilityTimeout;
            entry.DeliveryCount++;

            return Task.FromResult<LeasedMessage?>(new LeasedMessage(
                entry.LeaseId.Value,
                entry.Message,
                entry.DeliveryCount,
                entry.LeaseExpiresAt.Value
            ));
        }
    }

    public Task AckAsync(Guid leaseId, CancellationToken ct)
    {
        lock (_lock)
        {
            foreach (var q in _queues.Values)
            {
                var entry = q.FirstOrDefault(e => e.LeaseId == leaseId);
                if (entry != null)
                {
                    if (entry.State != "leased" || entry.LeaseExpiresAt <= DateTime.UtcNow)
                        throw new InvalidOperationException("Invalid lease");
                    q.Remove(entry);
                    return Task.CompletedTask;
                }
            }
            throw new InvalidOperationException("Lease not found");
        }
    }

    public Task NackAsync(Guid leaseId, bool requeue, CancellationToken ct)
    {
        lock (_lock)
        {
            foreach (var q in _queues.Values)
            {
                var entry = q.FirstOrDefault(e => e.LeaseId == leaseId);
                if (entry != null)
                {
                    if (entry.State != "leased" || entry.LeaseExpiresAt <= DateTime.UtcNow)
                        throw new InvalidOperationException("Invalid lease");

                    if (requeue)
                    {
                        entry.State = "available";
                        entry.LeaseId = null;
                        entry.LeaseExpiresAt = null;
                    }
                    else
                    {
                        entry.State = "dead_lettered";
                        entry.LeaseId = null;
                        entry.LeaseExpiresAt = null;
                    }
                    return Task.CompletedTask;
                }
            }
            throw new InvalidOperationException("Lease not found");
        }
    }

    public IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersAsync(string queueName, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

public class InMemoryRoutingStore : IRoutingStore
{
    private readonly ConcurrentDictionary<string, Exchange> _exchanges = new();
    private readonly ConcurrentDictionary<string, QueueDefinition> _queues = new();
    private readonly List<Binding> _bindings = new();
    private readonly object _lock = new();

    public Task DeclareExchangeAsync(Exchange exchange, CancellationToken ct)
    {
        _exchanges[exchange.Name] = exchange;
        return Task.CompletedTask;
    }

    public Task DeleteExchangeAsync(string exchangeName, CancellationToken ct)
    {
        _exchanges.TryRemove(exchangeName, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Exchange>> ListExchangesAsync(CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<Exchange>>(_exchanges.Values.ToList());
    }

    public Task DeclareQueueAsync(QueueDefinition queue, CancellationToken ct)
    {
        _queues[queue.Name] = queue;
        return Task.CompletedTask;
    }

    public Task DeleteQueueAsync(string queueName, CancellationToken ct)
    {
        _queues.TryRemove(queueName, out _);
        return Task.CompletedTask;
    }

    public Task<QueueDefinition?> GetQueueAsync(string queueName, CancellationToken ct)
    {
        _queues.TryGetValue(queueName, out var q);
        return Task.FromResult(q);
    }

    public Task<IReadOnlyList<QueueDefinition>> ListQueuesAsync(CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<QueueDefinition>>(_queues.Values.ToList());
    }

    public Task BindAsync(Binding binding, CancellationToken ct)
    {
        lock (_lock)
        {
            if (!_bindings.Any(b => b.ExchangeName == binding.ExchangeName && b.QueueName == binding.QueueName && b.RoutingKey == binding.RoutingKey))
            {
                _bindings.Add(binding);
            }
        }
        return Task.CompletedTask;
    }

    public Task UnbindAsync(string exchangeName, string queueName, string routingKey, CancellationToken ct)
    {
        lock (_lock)
        {
            _bindings.RemoveAll(b => b.ExchangeName == exchangeName && b.QueueName == queueName && b.RoutingKey == routingKey);
        }
        return Task.CompletedTask;
    }

    public Task<Exchange?> GetExchangeAsync(string exchangeName, CancellationToken ct)
    {
        _exchanges.TryGetValue(exchangeName, out var ex);
        return Task.FromResult(ex);
    }

    public Task<IReadOnlyList<Binding>> GetBindingsAsync(string exchangeName, CancellationToken ct)
    {
        lock (_lock)
        {
            var res = _bindings.Where(b => b.ExchangeName == exchangeName).ToList();
            return Task.FromResult<IReadOnlyList<Binding>>(res);
        }
    }
}
