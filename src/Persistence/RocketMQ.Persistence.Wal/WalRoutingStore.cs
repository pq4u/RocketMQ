using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Wal;

/// <summary>
/// WAL-backed implementation of <see cref="IRoutingStore"/> — persists
/// exchange, queue, and binding metadata using direct file writes.
///
/// TODO (design):
/// - Append-only metadata log with record types for each operation
///   (declare-exchange, delete-exchange, declare-queue, etc.)
/// - In-memory index rebuilt on startup from the log.
/// - Must satisfy the same 5-point contract as SqliteRoutingStore.
/// </summary>
public sealed class WalRoutingStore : IRoutingStore
{
    private readonly string _filePath;

    public WalRoutingStore(string filePath)
    {
        _filePath = filePath;
    }

    // Exchanges
    public Task DeclareExchangeAsync(Exchange exchange, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: append declare-exchange record to log, fsync, update in-memory index. " +
            "If name exists with different type/durable, throw InvalidOperationException. " +
            "(contract point 1)");

    public Task DeleteExchangeAsync(string exchangeName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: append delete-exchange record to log, fsync, remove exchange and its " +
            "bindings from in-memory index. No-op if not found. (contract point 3)");

    public Task<Exchange?> GetExchangeAsync(string exchangeName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: look up exchange by name in in-memory index, return null if not found");

    public Task<IReadOnlyList<Exchange>> ListExchangesAsync(CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: return all exchanges from in-memory index");

    // Queues
    public Task DeclareQueueAsync(QueueDefinition queue, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: append declare-queue record to log, fsync, update in-memory index. " +
            "If name exists with different durable/max_delivery_count, throw " +
            "InvalidOperationException. (contract point 1)");

    public Task DeleteQueueAsync(string queueName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: append delete-queue record to log, fsync, remove queue and its " +
            "bindings from in-memory index. No-op if not found. (contract point 3)");

    public Task<QueueDefinition?> GetQueueAsync(string queueName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: look up queue by name in in-memory index, return null if not found");

    public Task<IReadOnlyList<QueueDefinition>> ListQueuesAsync(CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: return all queues from in-memory index");

    // Bindings
    public Task BindAsync(Binding binding, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: verify exchange and queue exist in in-memory index (throw " +
            "InvalidOperationException if not — contract point 2). Append bind record " +
            "to log, fsync, update in-memory index.");

    public Task UnbindAsync(string exchangeName, string queueName, string routingKey, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: append unbind record to log, fsync, remove binding from in-memory " +
            "index. No-op if not found.");

    public Task<IReadOnlyList<Binding>> GetBindingsAsync(string exchangeName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: return all bindings for the given exchange from in-memory index");
}
