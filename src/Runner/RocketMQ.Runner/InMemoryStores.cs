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
    private enum MessageState
    {
        Available,
        Leased,
        DeadLettered
    }

    private sealed class MessageEntry
    {
        public Guid Id { get; init; }
        public InboundMessage Message { get; init; } = null!;
        public MessageState State { get; set; }
        public Guid? LeaseId { get; set; }
        public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
        public int DeliveryCount { get; set; }
        public long EnqueueSequence { get; init; }
        public DateTimeOffset? DeadLetteredAtUtc { get; set; }
        public string DeadLetterReason { get; set; } = string.Empty;
    }

    private readonly ConcurrentDictionary<string, List<MessageEntry>> _queues = new();
    private readonly IRoutingStore? _routingStore;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<Guid> _expiredLeaseIds = new();
    private readonly object _lock = new();
    private long _nextEnqueueSequence;

    public InMemoryMessageQueueStore(
        IRoutingStore? routingStore = null,
        TimeProvider? timeProvider = null)
    {
        _routingStore = routingStore;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

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
                State = MessageState.Available,
                EnqueueSequence = _nextEnqueueSequence++
            };

            _queues.GetOrAdd(queueName, static _ => new List<MessageEntry>()).Add(entry);
            return Task.FromResult(id);
        }
    }

    public async Task<LeasedMessage?> LeaseNextAsync(
        string queueName,
        TimeSpan visibilityTimeout,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateVisibilityTimeout(visibilityTimeout);

        var maxDeliveryCount = await GetMaxDeliveryCountAsync(queueName, ct);

        lock (_lock)
        {
            if (!_queues.TryGetValue(queueName, out var queue))
            {
                return null;
            }

            var now = _timeProvider.GetUtcNow();
            while (true)
            {
                var entry = queue
                    .Where(candidate => candidate.State == MessageState.Available ||
                                        (candidate.State == MessageState.Leased &&
                                         candidate.LeaseExpiresAtUtc <= now))
                    .OrderBy(candidate => candidate.EnqueueSequence)
                    .FirstOrDefault();

                if (entry == null)
                {
                    return null;
                }

                if (entry.State == MessageState.Leased && entry.LeaseId.HasValue)
                {
                    _expiredLeaseIds.Add(entry.LeaseId.Value);
                }

                if (maxDeliveryCount > 0 && entry.DeliveryCount >= maxDeliveryCount)
                {
                    MoveToDeadLetters(entry, now, "max-delivery-count-exceeded");
                    continue;
                }

                entry.State = MessageState.Leased;
                entry.LeaseId = Guid.NewGuid();
                entry.LeaseExpiresAtUtc = now + visibilityTimeout;
                entry.DeliveryCount++;

                return new LeasedMessage(
                    entry.Id,
                    entry.LeaseId.Value,
                    entry.Message,
                    entry.DeliveryCount,
                    entry.LeaseExpiresAtUtc.Value);
            }
        }
    }

    public Task AckAsync(Guid leaseId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            foreach (var queue in _queues.Values)
            {
                var entry = queue.FirstOrDefault(candidate => candidate.LeaseId == leaseId);
                if (entry == null)
                {
                    continue;
                }

                if (!HasActiveLease(entry, _timeProvider.GetUtcNow()))
                {
                    _expiredLeaseIds.Add(leaseId);
                    throw new InvalidOperationException("Lease expired");
                }

                queue.Remove(entry);
                return Task.CompletedTask;
            }

            throw new InvalidOperationException(
                _expiredLeaseIds.Contains(leaseId) ? "Lease expired" : "Lease not found");
        }
    }

    public Task NackAsync(Guid leaseId, bool requeue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            foreach (var queue in _queues.Values)
            {
                var entry = queue.FirstOrDefault(candidate => candidate.LeaseId == leaseId);
                if (entry == null)
                {
                    continue;
                }

                if (!HasActiveLease(entry, _timeProvider.GetUtcNow()))
                {
                    _expiredLeaseIds.Add(leaseId);
                    throw new InvalidOperationException("Lease expired");
                }

                if (requeue)
                {
                    entry.State = MessageState.Available;
                    entry.LeaseId = null;
                    entry.LeaseExpiresAtUtc = null;
                }
                else
                {
                    MoveToDeadLetters(entry, _timeProvider.GetUtcNow(), "consumer-rejected");
                }

                return Task.CompletedTask;
            }

            throw new InvalidOperationException(
                _expiredLeaseIds.Contains(leaseId) ? "Lease expired" : "Lease not found");
        }
    }

    public IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersAsync(
        string queueName,
        CancellationToken ct)
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
                    .Where(entry => entry.State == MessageState.DeadLettered)
                    .OrderBy(entry => entry.DeadLetteredAtUtc)
                    .Select(entry => new DeadLetteredMessage(
                        entry.Id,
                        entry.Message,
                        entry.DeliveryCount,
                        entry.DeadLetteredAtUtc ?? DateTimeOffset.MinValue,
                        entry.DeadLetterReason))
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

    private async Task<int> GetMaxDeliveryCountAsync(string queueName, CancellationToken ct)
    {
        if (_routingStore == null)
        {
            return 10;
        }

        var queue = await _routingStore.GetQueueAsync(queueName, ct);
        return queue?.MaxDeliveryCount ?? 10;
    }

    private static bool HasActiveLease(MessageEntry entry, DateTimeOffset now)
        => entry.State == MessageState.Leased &&
           entry.LeaseId.HasValue &&
           entry.LeaseExpiresAtUtc.HasValue &&
           entry.LeaseExpiresAtUtc.Value > now;

    private static void MoveToDeadLetters(
        MessageEntry entry,
        DateTimeOffset deadLetteredAtUtc,
        string reason)
    {
        entry.State = MessageState.DeadLettered;
        entry.LeaseId = null;
        entry.LeaseExpiresAtUtc = null;
        entry.DeadLetteredAtUtc = deadLetteredAtUtc;
        entry.DeadLetterReason = reason;
    }

    private static void ValidateVisibilityTimeout(TimeSpan visibilityTimeout)
    {
        if (visibilityTimeout <= TimeSpan.Zero || visibilityTimeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibilityTimeout),
                visibilityTimeout,
                "Visibility timeout must be greater than zero and finite.");
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
        if (queue.MaxDeliveryCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(queue),
                "MaxDeliveryCount cannot be negative.");
        }

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
