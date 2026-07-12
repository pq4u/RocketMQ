using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Sqlite;

/// <summary>
/// SQLite implementation of <see cref="IRoutingStore"/> — persists exchange,
/// queue, and binding metadata.
///
/// TODO (schema):
/// - Table: exchanges (name TEXT PK, type TEXT, durable INT)
/// - Table: queues (name TEXT PK, durable INT, max_delivery_count INT)
/// - Table: bindings (exchange_name TEXT, queue_name TEXT, routing_key TEXT,
///   PK = (exchange_name, queue_name, routing_key),
///   FK exchange_name → exchanges(name), FK queue_name → queues(name))
/// - Open with same connection as SqliteMessageQueueStore.
/// </summary>
public sealed class SqliteRoutingStore : IRoutingStore
{
    private readonly string _connectionString;

    public SqliteRoutingStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Exchanges
    public Task DeclareExchangeAsync(Exchange exchange, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: INSERT OR check existing config. If name exists with different " +
            "type/durable, throw InvalidOperationException. (contract point 1)");

    public Task DeleteExchangeAsync(string exchangeName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: DELETE FROM exchanges WHERE name = @name; also DELETE FROM bindings " +
            "WHERE exchange_name = @name. No-op if not found. (contract point 3)");

    public Task<Exchange?> GetExchangeAsync(string exchangeName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: SELECT * FROM exchanges WHERE name = @name");

    public Task<IReadOnlyList<Exchange>> ListExchangesAsync(CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: SELECT * FROM exchanges");

    // Queues
    public Task DeclareQueueAsync(QueueDefinition queue, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: INSERT OR check existing config. If name exists with different " +
            "durable/max_delivery_count, throw InvalidOperationException. (contract point 1)");

    public Task DeleteQueueAsync(string queueName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: DELETE FROM queues WHERE name = @name; also DELETE FROM bindings " +
            "WHERE queue_name = @name. No-op if not found. (contract point 3)");

    public Task<QueueDefinition?> GetQueueAsync(string queueName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: SELECT * FROM queues WHERE name = @name");

    public Task<IReadOnlyList<QueueDefinition>> ListQueuesAsync(CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: SELECT * FROM queues");

    // Bindings
    public Task BindAsync(Binding binding, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: Verify exchange and queue exist first (throw InvalidOperationException " +
            "if not — contract point 2). Then INSERT INTO bindings.");

    public Task UnbindAsync(string exchangeName, string queueName, string routingKey, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: DELETE FROM bindings WHERE exchange_name = @exchange " +
            "AND queue_name = @queue AND routing_key = @key. No-op if not found.");

    public Task<IReadOnlyList<Binding>> GetBindingsAsync(string exchangeName, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: SELECT * FROM bindings WHERE exchange_name = @name");
}
