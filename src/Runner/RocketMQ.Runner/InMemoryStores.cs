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
        public DateTimeOffset? DeadLetteredAtUtc { get; set; }
        public string DeadLetterReason { get; set; } = string.Empty;
    }

    private readonly ConcurrentDictionary<string, List<MessageEntry>> _queues = new();
    private readonly object _lock = new();

    public Task<Guid> EnqueueAsync(string queueName, InboundMessage message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var id = Guid.NewGuid();
            var entry = new MessageEntry
            {
                Id = id,
                Message = message,
                EnqueuedAt = DateTime.UtcNow
            };
            if (!_queues.ContainsKey(queueName))
            {
                _queues[queueName] = new List<MessageEntry>();
            }

            _queues[queueName].Add(entry);
            return Task.FromResult(id);
        }
    }

    public Task<LeasedMessage?> LeaseNextAsync(string queueName, TimeSpan visibilityTimeout, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_queues.TryGetValue(queueName, out var queue))
            {
                return Task.FromResult<LeasedMessage?>(null);
            }

            var now = DateTime.UtcNow;
            var entry = queue
                .Where(e => e.State == "available" || (e.State == "leased" && e.LeaseExpiresAt < now))
                .OrderBy(e => e.EnqueuedAt)
                .FirstOrDefault();

            if (entry == null)
            {
                return Task.FromResult<LeasedMessage?>(null);
            }

            entry.State = "leased";
            entry.LeaseId = Guid.NewGuid();
            entry.LeaseExpiresAt = now + visibilityTimeout;
            entry.DeliveryCount++;

            return Task.FromResult<LeasedMessage?>(new LeasedMessage(
                entry.LeaseId.Value,
                entry.Message,
                entry.DeliveryCount,
                entry.LeaseExpiresAt.Value));
        }
    }

    public Task AckAsync(Guid leaseId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            foreach (var queue in _queues.Values)
            {
                var entry = queue.FirstOrDefault(e => e.LeaseId == leaseId);
                if (entry == null)
                {
                    continue;
                }

                if (entry.State != "leased" || entry.LeaseExpiresAt <= DateTime.UtcNow)
                {
                    throw new InvalidOperationException("Invalid lease");
                }

                queue.Remove(entry);
                return Task.CompletedTask;
            }

            throw new InvalidOperationException("Lease not found");
        }
    }

    public Task NackAsync(Guid leaseId, bool requeue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            foreach (var queue in _queues.Values)
            {
                var entry = queue.FirstOrDefault(e => e.LeaseId == leaseId);
                if (entry == null)
                {
                    continue;
                }

                if (entry.State != "leased" || entry.LeaseExpiresAt <= DateTime.UtcNow)
                {
                    throw new InvalidOperationException("Invalid lease");
                }

                entry.LeaseId = null;
                entry.LeaseExpiresAt = null;
                if (requeue)
                {
                    entry.State = "available";
                }
                else
                {
                    entry.State = "dead_lettered";
                    entry.DeadLetteredAtUtc = DateTimeOffset.UtcNow;
                }

                return Task.CompletedTask;
            }

            throw new InvalidOperationException("Lease not found");
        }
    }

    public IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersAsync(string queueName, CancellationToken ct)
        => BrowseDeadLettersCoreAsync(queueName, ct);

    private async IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersCoreAsync(
        string queueName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        List<DeadLetteredMessage> deadLetters;
        lock (_lock)
        {
            deadLetters = _queues.TryGetValue(queueName, out var entries)
                ? entries
                    .Where(e => e.State == "dead_lettered")
                    .OrderBy(e => e.DeadLetteredAtUtc)
                    .Select(e => new DeadLetteredMessage(
                        e.Id,
                        e.Message,
                        e.DeliveryCount,
                        e.DeadLetteredAtUtc ?? DateTimeOffset.MinValue,
                        e.DeadLetterReason))
                    .ToList()
                : new List<DeadLetteredMessage>();
        }

        foreach (var deadLetter in deadLetters)
        {
            ct.ThrowIfCancellationRequested();
            yield return deadLetter;
            await Task.Yield();
        }
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
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_exchanges.TryGetValue(exchange.Name, out var existing))
            {
                if (existing.Type != exchange.Type || existing.Durable != exchange.Durable)
                {
                    throw new InvalidOperationException(
                        $"Exchange '{exchange.Name}' exists with different configuration.");
                }

                return Task.CompletedTask;
            }

            _exchanges[exchange.Name] = exchange;
            return Task.CompletedTask;
        }
    }

    public Task DeleteExchangeAsync(string exchangeName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _exchanges.TryRemove(exchangeName, out _);
            _bindings.RemoveAll(binding => binding.ExchangeName == exchangeName);
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<Exchange>> ListExchangesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Exchange>>(_exchanges.Values.ToList());
        }
    }

    public Task DeclareQueueAsync(QueueDefinition queue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_queues.TryGetValue(queue.Name, out var existing))
            {
                if (existing.Durable != queue.Durable ||
                    existing.MaxDeliveryCount != queue.MaxDeliveryCount)
                {
                    throw new InvalidOperationException(
                        $"Queue '{queue.Name}' exists with different configuration.");
                }

                return Task.CompletedTask;
            }

            _queues[queue.Name] = queue;
            return Task.CompletedTask;
        }
    }

    public Task DeleteQueueAsync(string queueName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _queues.TryRemove(queueName, out _);
            _bindings.RemoveAll(binding => binding.QueueName == queueName);
            return Task.CompletedTask;
        }
    }

    public Task<QueueDefinition?> GetQueueAsync(string queueName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _queues.TryGetValue(queueName, out var queue);
            return Task.FromResult(queue);
        }
    }

    public Task<IReadOnlyList<QueueDefinition>> ListQueuesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<QueueDefinition>>(_queues.Values.ToList());
        }
    }

    public Task BindAsync(Binding binding, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_exchanges.ContainsKey(binding.ExchangeName))
            {
                throw new InvalidOperationException(
                    $"Exchange '{binding.ExchangeName}' does not exist.");
            }

            if (!_queues.ContainsKey(binding.QueueName))
            {
                throw new InvalidOperationException(
                    $"Queue '{binding.QueueName}' does not exist.");
            }

            if (!_bindings.Any(b => b.ExchangeName == binding.ExchangeName &&
                                    b.QueueName == binding.QueueName &&
                                    b.RoutingKey == binding.RoutingKey))
            {
                _bindings.Add(binding);
            }

            return Task.CompletedTask;
        }
    }

    public Task UnbindAsync(string exchangeName, string queueName, string routingKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _bindings.RemoveAll(b => b.ExchangeName == exchangeName &&
                                     b.QueueName == queueName &&
                                     b.RoutingKey == routingKey);
            return Task.CompletedTask;
        }
    }

    public Task<Exchange?> GetExchangeAsync(string exchangeName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _exchanges.TryGetValue(exchangeName, out var exchange);
            return Task.FromResult(exchange);
        }
    }

    public Task<IReadOnlyList<Binding>> GetBindingsAsync(string exchangeName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            var result = _bindings.Where(b => b.ExchangeName == exchangeName).ToList();
            return Task.FromResult<IReadOnlyList<Binding>>(result);
        }
    }
}